(function () {
    'use strict';

    const pluginId = 'b3ff3bcd-9b77-4a5e-9c22-3c5236757d12';
    const pageClass = 'uploadInboxPage';
    const chunkSizeBytes = 8 * 1024 * 1024;

    // WeakMap prevents leaks when Jellyfin removes the page element.
    const pageState = new WeakMap();

    function formatTargetLabel(target) {
        var libraryName = target.displayName || target.DisplayName || 'Library';
        var destination = target.destinationDisplayName || target.DestinationDisplayName || 'Library root';
        return libraryName + ' — ' + destination;
    }

    function updateTargetHint(page, targets) {
        var select = page.querySelector('.selectTarget');
        var hint = page.querySelector('.selectedTargetHint');
        if (!select || !hint) {
            return;
        }

        var selectedId = select.value;
        var target = (targets || []).find(function (t) { return t.id === selectedId || t.Id === selectedId; });
        if (!target) {
            hint.textContent = 'Files will be uploaded to the selected library destination.';
            return;
        }

        var destination = target.destinationDisplayName || target.DestinationDisplayName || 'Library root';
        hint.textContent = 'Files will be uploaded to: ' + destination;
    }

    function loadTargets(page) {
        ApiClient.ajax({
            type: 'GET',
            url: ApiClient.getUrl('uploadinbox/targets'),
            dataType: 'json'
        }).then(function (targets) {
            targets = targets || [];
            var select = page.querySelector('.selectTarget');
            select.innerHTML = '';
            targets.forEach(function (t) {
                var opt = document.createElement('option');
                opt.value = t.id || t.Id;
                opt.textContent = formatTargetLabel(t);
                select.appendChild(opt);
            });

            page.availableTargets = targets;
            updateTargetHint(page, targets);
        }, function () {
            var hint = page.querySelector('.selectedTargetHint');
            if (hint) {
                hint.textContent = 'Failed to load upload targets.';
            }
        });
    }

    function createUploadItem(file) {
        var div = document.createElement('div');
        div.className = 'uploadItem';
        div.innerHTML =
            '<div>' + file.name + ' (' + Math.round(file.size / (1024 * 1024)) + ' MB)</div>' +
            '<div class="uploadProgressBar"><div class="uploadProgressBarInner"></div></div>' +
            '<div class="uploadStatus"></div>';
        return div;
    }

    function setProgress(item, fraction) {
        item.querySelector('.uploadProgressBarInner').style.width = (fraction * 100) + '%';
    }

    function setStatus(item, text) {
        item.querySelector('.uploadStatus').textContent = text;
    }

    function normaliseAjaxJsonResponse(resp) {
        // Jellyfin ApiClient.ajax can return:
        //  - an object (already parsed JSON)
        //  - a JSON string
        //  - an XHR-ish object with responseText
        if (resp == null) return null;

        if (typeof resp === 'string') {
            try { return JSON.parse(resp); } catch (_) { return null; }
        }

        if (resp.responseText && typeof resp.responseText === 'string') {
            try { return JSON.parse(resp.responseText); } catch (_) { return null; }
        }

        return resp;
    }

    async function createSession(targetId, file) {
        var url = ApiClient.getUrl('uploadinbox/uploads');

        var raw = await ApiClient.ajax({
            type: 'POST',
            url: url,
            data: JSON.stringify({
                targetId: targetId,
                fileName: file.name,
                totalBytes: file.size,
                contentType: file.type || null
            }),
            contentType: 'application/json',
            dataType: 'json' // best-effort hint
        });

        var session = normaliseAjaxJsonResponse(raw);

        // Accept both casings, since server/client combos differ.
        var uploadId = session && (session.uploadId || session.UploadId || session.id);
        if (!uploadId) {
            // Make this loud in the UI + console so we don't silently proceed to /undefined
            console.error('Create session returned unexpected payload:', raw);
            throw new Error('Create upload session did not return uploadId');
        }

        // Return a normalised object so the rest of the code is stable
        return {
            uploadId: uploadId,
            chunkSize: session.chunkSize || session.ChunkSize,
            maxFileSizeBytes: session.maxFileSizeBytes || session.MaxFileSizeBytes,
            receivedBytes: session.receivedBytes || session.ReceivedBytes || 0
        };
    }

    function uploadChunk(uploadId, start, end, total, blob) {
        var url = ApiClient.getUrl('uploadinbox/uploads/' + encodeURIComponent(uploadId));
        var xhr = new XMLHttpRequest();
        xhr.open('PATCH', url, true);
        xhr.setRequestHeader('Content-Range', 'bytes ' + start + '-' + (end - 1) + '/' + total);

        var token = typeof ApiClient.accessToken === 'function'
            ? ApiClient.accessToken()
            : ApiClient.accessToken;
        if (token) {
            xhr.setRequestHeader('Authorization', 'MediaBrowser Token="' + token + '"');
        }

        return new Promise(function (resolve, reject) {
            xhr.onload = function () {
                if (xhr.status >= 200 && xhr.status < 300) {
                    try { resolve(JSON.parse(xhr.responseText || '{}')); } catch (_) { resolve({}); }
                } else {
                    reject(xhr);
                }
            };
            xhr.onerror = function () { reject(xhr); };
            xhr.send(blob);
        });
    }

    function finaliseUpload(uploadId) {
        var url = ApiClient.getUrl('uploadinbox/uploads/' + encodeURIComponent(uploadId) + '/finalise');
        return ApiClient.ajax({ type: 'POST', url: url });
    }

    async function uploadFile(page, targetId, file, item) {
        try {
            setStatus(item, 'Starting...');

            var session = await createSession(targetId, file);
            var uploadId = session.uploadId;
            var chunkSize = session.chunkSize || chunkSizeBytes;
            var maxFileSizeBytes = session.maxFileSizeBytes;

            if (maxFileSizeBytes && file.size > maxFileSizeBytes) {
                setStatus(item, 'File is larger than server limit.');
                return;
            }

            var offset = session.receivedBytes || 0;
            var total = file.size;

            while (offset < total) {
                var next = Math.min(offset + chunkSize, total);
                await uploadChunk(uploadId, offset, next, total, file.slice(offset, next));
                offset = next;
                setProgress(item, offset / total);
                setStatus(item, 'Uploaded ' + Math.round(offset / (1024 * 1024)) + ' / ' + Math.round(total / (1024 * 1024)) + ' MB');
            }

            await finaliseUpload(uploadId);
            setProgress(item, 1);
            setStatus(item, 'Completed');
        } catch (err) {
            var message = 'Upload failed.';
            if (err && err.message) {
                message = err.message;
            } else if (err && err.status === 403) {
                message = "You don't have permission to upload.";
            } else if (err && err.status === 413) {
                message = 'File too large. The reverse proxy or server may limit request size.';
            } else if (err && err.status === 507) {
                message = 'Server disk is full or quota exceeded.';
            }
            setStatus(item, message);
        }
    }

    function handleFiles(page, files) {
        var targetId = page.querySelector('.selectTarget').value;
        if (!targetId) {
            alert('Please select a target first.');
            return;
        }
        var queue = page.querySelector('.uploadQueue');
        Array.from(files).forEach(function (file) {
            var item = createUploadItem(file);
            queue.appendChild(item);
            uploadFile(page, targetId, file, item);
        });
    }

    function init(page) {
        if (pageState.has(page)) return;

        var abort = new AbortController();
        pageState.set(page, { abort: abort });

        loadTargets(page);

        var zone = page.querySelector('.uploadDropZone');
        zone.addEventListener('dragover', function (e) {
            e.preventDefault();
            e.stopPropagation();
            zone.classList.add('dragover');
        }, { signal: abort.signal });
        zone.addEventListener('dragleave', function (e) {
            e.preventDefault();
            e.stopPropagation();
            zone.classList.remove('dragover');
        }, { signal: abort.signal });
        zone.addEventListener('drop', function (e) {
            e.preventDefault();
            e.stopPropagation();
            zone.classList.remove('dragover');
            if (e.dataTransfer.files && e.dataTransfer.files.length) {
                handleFiles(page, e.dataTransfer.files);
            }
        }, { signal: abort.signal });

        var fileInput = page.querySelector('#fileInput');
        fileInput.addEventListener('change', function (e) {
            if (e.target.files && e.target.files.length) {
                handleFiles(page, e.target.files);
            }
        }, { signal: abort.signal });

        var select = page.querySelector('.selectTarget');
        select.addEventListener('change', function () {
            updateTargetHint(page, page.availableTargets || []);
        }, { signal: abort.signal });
    }

    function destroy(page) {
        var state = pageState.get(page);
        if (state && state.abort) {
            state.abort.abort();
        }
        pageState.delete(page);
    }

    document.addEventListener('viewshow', function (e) {
        var page = e && e.target ? e.target : null;
        if (page && page.classList.contains(pageClass)) {
            init(page);
        }
    });

    document.addEventListener('viewhide', function (e) {
        var page = e && e.target ? e.target : null;
        if (page && page.classList.contains(pageClass)) {
            destroy(page);
        }
    });

    // Fallback for cases where Jellyfin uses page events instead of view events.
    document.addEventListener('pageshow', function (e) {
        var page = e && e.target ? e.target : null;
        if (page && page.id === 'UploadInboxPage') {
            init(page);
        }
    });

    document.addEventListener('pagehide', function (e) {
        var page = e && e.target ? e.target : null;
        if (page && page.id === 'UploadInboxPage') {
            destroy(page);
        }
    });
})();
