(function () {
    'use strict';

    const pluginId = 'b3ff3bcd-9b77-4a5e-9c22-3c5236757d12';
    const pageId = 'UploadInboxConfigPage';

    // Track per-page instance resources to avoid stale bindings across navigation.
    const pageState = new WeakMap(); // page -> { abort: AbortController }

    function getLibraryRoots(page) {
        return page.availableLibraryRoots || [];
    }

    function getTargetsContainer(page) {
        return page.querySelector('.uploadTargetsContainer');
    }

    function appendLabel(container, text) {
        const label = document.createElement('label');
        label.textContent = text;
        container.appendChild(label);
    }

    function appendTextInput(container, className, value) {
        const input = document.createElement('input');
        input.setAttribute('is', 'emby-input');
        input.type = 'text';
        input.className = className;
        input.value = value || '';
        container.appendChild(input);
        return input;
    }

    function appendNumberInput(container, className, value, min) {
        const input = document.createElement('input');
        input.setAttribute('is', 'emby-input');
        input.type = 'number';
        input.className = className;
        if (min != null) input.min = String(min);
        input.value = String(value ?? '');
        container.appendChild(input);
        return input;
    }

    function appendCheckbox(container, className, checked) {
        const input = document.createElement('input');
        input.setAttribute('is', 'emby-checkbox');
        input.type = 'checkbox';
        input.className = className;
        input.checked = Boolean(checked);
        container.appendChild(input);
        return input;
    }

    function appendButton(container, className, text) {
        const btn = document.createElement('button');
        btn.setAttribute('is', 'emby-button');
        btn.type = 'button';
        btn.className = className + ' emby-button';

        const span = document.createElement('span');
        span.textContent = text;
        btn.appendChild(span);

        container.appendChild(btn);
        return btn;
    }

    function appendText(container, text) {
        const div = document.createElement('div');
        div.textContent = text;
        container.appendChild(div);
        return div;
    }

    function encodeLibraryRoot(root) {
        return JSON.stringify(root);
    }

    function appendSelect(container, className, options, value) {
        const select = document.createElement('select');
        select.setAttribute('is', 'emby-select');
        select.className = className;
        (options || []).forEach(function (opt) {
            const o = document.createElement('option');
            o.value = opt.value;
            o.textContent = opt.text;
            select.appendChild(o);
        });
        if (value != null) select.value = value;
        container.appendChild(select);
        return select;
    }

    function createLibraryRootOptions(page) {
        const roots = getLibraryRoots(page);
        const options = roots.map(function (root) {
            return {
                value: encodeLibraryRoot(root),
                text: (root.libraryName || 'Library') + ' — ' + root.libraryPath
            };
        });

        if (!options.length) {
            options.push({
                value: '',
                text: 'No Jellyfin library roots found'
            });
        }

        return options;
    }

    function getTargetSelectionValue(target) {
        return target.LibraryId && target.LibraryPath ? encodeLibraryRoot({ libraryId: target.LibraryId, libraryName: target.LibraryName || '', libraryPath: target.LibraryPath }) : '';
    }

    function createFieldContainer(parent) {
        const c = document.createElement('div');
        c.className = 'inputContainer';
        parent.appendChild(c);
        return c;
    }

    function createTargetRow(page, target, index) {
        const row = document.createElement('div');
        row.className = 'uploadTargetRow';

        const fields = document.createElement('div');
        fields.className = 'uploadTargetFields';
        row.appendChild(fields);

        // Library root
        {
            const c = createFieldContainer(fields);
            appendLabel(c, 'Library');
            appendSelect(c, 'selLibraryRoot', createLibraryRootOptions(page), getTargetSelectionValue(target));
        }

        // Upload subfolder
        {
            const c = createFieldContainer(fields);
            appendLabel(c, 'Upload folder');
            appendTextInput(c, 'txtUploadSubdirectory', target.UploadSubdirectory || '');
            appendText(c, 'Optional. Example: incoming');
        }

        // Who can upload?
        {
            const c = createFieldContainer(fields);
            appendLabel(c, 'Who can upload?');

            const mode = target.AccessMode || 'AllUsers';
            appendSelect(c, 'selAccessMode', [
                { value: 'AllUsers', text: 'All users (default)' },
                { value: 'AdminsOnly', text: 'Admins only' }
            ], mode);
        }

        // Max size
        {
            const c = createFieldContainer(fields);
            const defaultMaxBytes = 20 * 1024 * 1024 * 1024;
            const maxBytes = target.MaxFileSizeBytes || defaultMaxBytes;
            const maxGb = Math.round(maxBytes / (1024 * 1024 * 1024));
            appendLabel(c, 'Max file size (GB)');
            appendNumberInput(c, 'txtMaxSize', maxGb, 1);
        }

        // Allowed extensions
        {
            const c = createFieldContainer(fields);
            appendLabel(c, 'Allowed extensions (comma separated, empty = all)');
            appendTextInput(c, 'txtExtensions', (target.AllowedExtensions || []).join(', '));
        }

        // Actions
        const actions = document.createElement('div');
        actions.className = 'uploadTargetActions';
        row.appendChild(actions);

        const del = appendButton(actions, 'raised button-deleteTarget', 'Delete target');
        del.setAttribute('data-index', String(index));

        return row;
    }

    function loadConfiguration(page, config) {
        const container = getTargetsContainer(page);
        container.innerHTML = '';

        const targets = config.Targets || [];
        targets.forEach((t, i) => {
            container.appendChild(createTargetRow(page, t, i));
        });
    }

    function validateExtensionEntry(ext) {
        const t = (ext || '').trim();
        if (t.length === 0) return { valid: true, normalized: null };
        if (/[\s/\\]/.test(t)) return { valid: false };
        const normalized = t.startsWith('.') ? t.slice(1) : t;
        if (normalized.length === 0) return { valid: false };
        return { valid: true, normalized: normalized };
    }

    function validateSubdirectoryEntry(value) {
        const raw = (value || '').trim();
        if (raw.length === 0) {
            return { valid: true, normalized: '' };
        }

        if (/^[\\/]/.test(raw) || /^[A-Za-z]:/.test(raw)) {
            return { valid: false };
        }

        const segments = raw.split(/[\\/]+/).filter(Boolean);
        if (!segments.length) {
            return { valid: true, normalized: '' };
        }

        for (let i = 0; i < segments.length; i++) {
            const segment = segments[i].trim();
            if (!segment || segment === '.' || segment === '..') {
                return { valid: false };
            }

            if (/[<>:"|?*\u0000]/.test(segment)) {
                return { valid: false };
            }
        }

        return {
            valid: true,
            normalized: segments.join('/')
        };
    }

    function normaliseAjaxJsonResponse(resp) {
        return (typeof resp === 'string') ? JSON.parse(resp) : resp;
    }

    function validateTargetsFromPage(page, config) {
        const container = getTargetsContainer(page);
        const rows = container.querySelectorAll('.uploadTargetRow');
        const errors = [];

        rows.forEach((row, index) => {
            const selectedRoot = row.querySelector('.selLibraryRoot').value || '';
            const extsInput = row.querySelector('.txtExtensions').value || '';
            const subdirectoryInput = row.querySelector('.txtUploadSubdirectory').value || '';

            if (selectedRoot.length === 0) {
                errors.push('Target ' + (index + 1) + ': Select a Jellyfin library folder.');
            } else {
                try {
                    const parsed = JSON.parse(selectedRoot);
                    if (!parsed.libraryId || !parsed.libraryPath) {
                        errors.push('Target ' + (index + 1) + ': Invalid library folder selection.');
                    }
                } catch (_) {
                    errors.push('Target ' + (index + 1) + ': Invalid library folder selection.');
                }
            }

            const subdirectory = validateSubdirectoryEntry(subdirectoryInput);
            if (!subdirectory.valid) {
                errors.push('Target ' + (index + 1) + ': Upload folder must be a relative path inside the selected library, for example incoming or movies/incoming.');
            }

            const extParts = extsInput.split(',').map(e => e.trim()).filter(e => e.length > 0);
            for (let i = 0; i < extParts.length; i++) {
                const r = validateExtensionEntry(extParts[i]);
                if (!r.valid) {
                    errors.push('Target ' + (index + 1) + ': Invalid extension entry (no spaces or path characters; e.g. mp4, mkv).');
                    break;
                }
            }
        });

        return errors;
    }

    function readConfigurationFromPage(page, config) {
        const targets = [];
        const container = getTargetsContainer(page);
        const rows = container.querySelectorAll('.uploadTargetRow');

        rows.forEach((row, index) => {
            const id = (config.Targets && config.Targets[index] && config.Targets[index].Id) || null;
            const selectedRootRaw = row.querySelector('.selLibraryRoot')?.value || '';
            let selectedRoot = null;
            try { selectedRoot = selectedRootRaw ? JSON.parse(selectedRootRaw) : null; } catch (_) { selectedRoot = null; }
            const accessMode = row.querySelector('.selAccessMode')?.value || 'AllUsers';
            const maxGb = parseInt(row.querySelector('.txtMaxSize').value || '20', 10);
            const maxBytes = maxGb * 1024 * 1024 * 1024;
            const uploadSubdirectoryInput = row.querySelector('.txtUploadSubdirectory')?.value || '';
            const uploadSubdirectory = validateSubdirectoryEntry(uploadSubdirectoryInput);
            const exts = row.querySelector('.txtExtensions').value || '';

            const allowedExtensions = exts
                .split(',')
                .map(e => e.trim())
                .filter(e => e.length > 0)
                .map(e => {
                    const r = validateExtensionEntry(e);
                    return r.valid && r.normalized ? r.normalized : null;
                })
                .filter(Boolean);

            targets.push({
                Id: id || null,
                LibraryId: selectedRoot && selectedRoot.libraryId ? selectedRoot.libraryId : '',
                LibraryName: selectedRoot && selectedRoot.libraryName ? selectedRoot.libraryName : '',
                LibraryPath: selectedRoot && selectedRoot.libraryPath ? selectedRoot.libraryPath : '',
                UploadSubdirectory: uploadSubdirectory.valid ? uploadSubdirectory.normalized : '',
                AccessMode: accessMode,
                MaxFileSizeBytes: maxBytes,
                AllowedExtensions: allowedExtensions,
            });
        });

        config.Targets = targets;
    }

    function ensureTargetIds(config) {
        config.Targets = config.Targets || [];
        config.Targets.forEach(t => {
            if (!t.Id) {
                t.Id = (window.crypto && window.crypto.randomUUID)
                    ? window.crypto.randomUUID()
                    : Date.now().toString();
            }
        });
    }

    function loadLibraryRoots() {
        return ApiClient.ajax({
            type: 'GET',
            url: ApiClient.getUrl('Library/VirtualFolders'),
            dataType: 'json'
        }).then(function (resp) {
            const raw = Array.isArray(resp) ? resp : [];
            return raw.flatMap(function (folder) {
                const libraryId = folder.ItemId || folder.itemId || '';
                const libraryName = folder.Name || folder.name || '';
                const locations = folder.Locations || folder.locations || [];
                return locations.map(function (libraryPath) {
                    return { libraryId: libraryId, libraryName: libraryName, libraryPath: libraryPath };
                });
            }).filter(function (x) { return x.libraryId && x.libraryPath; });
        });
    }

    function validateTargetOnServer(target) {
        return ApiClient.ajax({
            type: 'POST',
            url: ApiClient.getUrl('uploadinbox/validate-target'),
            data: JSON.stringify(target),
            contentType: 'application/json',
            dataType: 'json'
        }).then(normaliseAjaxJsonResponse);
    }

    function validateTargetsOnServer(targets) {
        return Promise.all((targets || []).map(function (target, index) {
            return validateTargetOnServer(target).then(function (result) {
                return {
                    index: index,
                    result: result || { isValid: false, error: 'Validation failed.' }
                };
            });
        }));
    }

    function loadFromServer(page) {
        Dashboard.showLoadingMsg();
        Promise.all([
            ApiClient.getPluginConfiguration(pluginId),
            loadLibraryRoots()
        ]).then(function (results) {
            const config = results[0] || {};
            page.availableLibraryRoots = results[1] || [];
            ensureTargetIds(config);
            page.currentConfig = config;
            loadConfiguration(page, config);
            Dashboard.hideLoadingMsg();
        }, function () {
            Dashboard.hideLoadingMsg();
            Dashboard.alert('Failed to load Upload Inbox configuration or Jellyfin libraries.');
        });
    }

    function onSubmit(page, e) {
        e.preventDefault();
        const config = page.currentConfig || {};
        readConfigurationFromPage(page, config);

        const errors = validateTargetsFromPage(page, config);
        if (errors.length > 0) {
            Dashboard.alert(errors.join('\n'));
            return;
        }

        Dashboard.showLoadingMsg();
        validateTargetsOnServer(config.Targets || []).then(function (results) {
            const failures = results.filter(function (x) {
                return !(x.result && (x.result.isValid || x.result.IsValid));
            });

            if (failures.length > 0) {
                Dashboard.hideLoadingMsg();
                Dashboard.alert(failures.map(function (x) {
                    const error = (x.result && (x.result.error || x.result.Error)) || 'Validation failed.';
                    return 'Target ' + (x.index + 1) + ': ' + error;
                }).join('\n'));
                return;
            }

            ApiClient.updatePluginConfiguration(pluginId, config).then(() => {
                Dashboard.processPluginConfigurationUpdateResult();
                Dashboard.hideLoadingMsg();
            }, () => {
                Dashboard.hideLoadingMsg();
                Dashboard.alert('Failed to save Upload Inbox configuration.');
            });
        }, function () {
            Dashboard.hideLoadingMsg();
            Dashboard.alert('Failed to validate upload targets.');
        });
    }

    function onAddTargetClick(page) {
        const config = page.currentConfig || { Targets: [] };
        config.Targets = config.Targets || [];
        const firstRoot = (page.availableLibraryRoots || [])[0] || null;
        config.Targets.push({
            Id: (window.crypto && window.crypto.randomUUID) ? window.crypto.randomUUID() : Date.now().toString(),
            LibraryId: firstRoot ? firstRoot.libraryId : '',
            LibraryName: firstRoot ? firstRoot.libraryName : '',
            LibraryPath: firstRoot ? firstRoot.libraryPath : '',
            UploadSubdirectory: '',
            AccessMode: 'AllUsers',
            MaxFileSizeBytes: 20 * 1024 * 1024 * 1024,
            AllowedExtensions: [],
        });
        page.currentConfig = config;
        loadConfiguration(page, config);
    }

    function onDeleteTargetClick(page, button) {
        const index = parseInt(button.getAttribute('data-index'), 10);
        const config = page.currentConfig || { Targets: [] };
        config.Targets.splice(index, 1);
        page.currentConfig = config;
        loadConfiguration(page, config);
    }

    function init(page) {
        if (!page || pageState.has(page)) {
            // Already initialised for this page instance.
            return;
        }

        const abort = new AbortController();
        pageState.set(page, { abort });

        // Delegate all clicks within the page so re-rendering doesn't break handlers.
        page.addEventListener('click', function (e) {
            const add = e.target && e.target.closest ? e.target.closest('.button-addTarget') : null;
            if (add) {
                onAddTargetClick(page);
                return;
            }

            const del = e.target && e.target.closest ? e.target.closest('.button-deleteTarget') : null;
            if (del) {
                onDeleteTargetClick(page, del);
            }
        }, { signal: abort.signal });

        const form = page.querySelector('#UploadInboxConfigForm');
        if (form) {
            form.addEventListener('submit', function (evt) {
                onSubmit(page, evt);
            }, { signal: abort.signal });
        }

        loadFromServer(page);
    }

    function destroy(page) {
        const state = pageState.get(page);
        if (state && state.abort) {
            state.abort.abort();
        }
        pageState.delete(page);
    }

    // Dashboard lifecycle: initialise the active instance on viewshow.
    document.addEventListener('viewshow', function (e) {
        const page = e && e.target ? e.target : null;
        if (page && page.id === pageId) {
            init(page);
        }
    });

    // Optional but recommended: teardown on viewhide to prevent leaks in long sessions.
    document.addEventListener('viewhide', function (e) {
        const page = e && e.target ? e.target : null;
        if (page && page.id === pageId) {
            destroy(page);
        }
    });
})();
