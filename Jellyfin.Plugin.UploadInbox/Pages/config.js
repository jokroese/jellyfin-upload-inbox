(function () {
    'use strict';

    const pluginId = 'b3ff3bcd-9b77-4a5e-9c22-3c5236757d12';
    const pageId = 'UploadInboxConfigPage';

    // Track per-page instance resources to avoid stale bindings across navigation.
    const pageState = new WeakMap(); // page -> { abort: AbortController }

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

    function createTargetRow(target, index) {
        const div = document.createElement('div');
        div.className = 'inputContainer';

        appendLabel(div, 'Display name');
        appendTextInput(div, 'txtDisplayName', target.DisplayName);

        appendLabel(div, 'Base path (on server)');
        appendTextInput(div, 'txtBasePath', target.BasePath);

        appendLabel(div, 'Create per-user subfolder');
        appendCheckbox(div, 'chkUserSubfolder', target.CreateUserSubfolder);

        const defaultMaxBytes = 20 * 1024 * 1024 * 1024;
        const maxBytes = target.MaxFileSizeBytes || defaultMaxBytes;
        const maxGb = Math.round(maxBytes / (1024 * 1024 * 1024));
        appendLabel(div, 'Max file size (GB)');
        appendNumberInput(div, 'txtMaxSize', maxGb, 1);

        appendLabel(div, 'Allowed extensions (comma separated, empty = all)');
        appendTextInput(div, 'txtExtensions', (target.AllowedExtensions || []).join(', '));

        appendLabel(div, 'Admin-only: Allowed user IDs (comma separated GUIDs)');
        appendTextInput(div, 'txtUsers', (target.AllowedUserIds || []).join(', '));

        const del = appendButton(div, 'raised button-deleteTarget', 'Delete target');
        del.setAttribute('data-index', String(index));

        div.appendChild(document.createElement('hr'));
        return div;
    }

    function loadConfiguration(page, config) {
        const container = getTargetsContainer(page);
        container.innerHTML = '';

        const targets = config.Targets || [];
        targets.forEach((t, i) => {
            container.appendChild(createTargetRow(t, i));
        });
    }

    function readConfigurationFromPage(page, config) {
        const targets = [];
        const container = getTargetsContainer(page);
        const rows = container.querySelectorAll('.inputContainer');

        rows.forEach((row, index) => {
            const id = (config.Targets && config.Targets[index] && config.Targets[index].Id) || null;
            const displayName = row.querySelector('.txtDisplayName').value || '';
            const basePath = row.querySelector('.txtBasePath').value || '';
            const createUserSubfolder = row.querySelector('.chkUserSubfolder').checked;
            const maxGb = parseInt(row.querySelector('.txtMaxSize').value || '20', 10);
            const maxBytes = maxGb * 1024 * 1024 * 1024;
            const exts = row.querySelector('.txtExtensions').value || '';
            const usersRaw = row.querySelector('.txtUsers').value || '';

            const allowedExtensions = exts
                .split(',')
                .map(e => e.trim())
                .filter(e => e.length > 0);

            const allowedUserIds = usersRaw
                .split(',')
                .map(e => e.trim())
                .filter(e => e.length > 0);

            targets.push({
                Id: id || null,
                DisplayName: displayName,
                BasePath: basePath,
                CreateUserSubfolder: createUserSubfolder,
                MaxFileSizeBytes: maxBytes,
                AllowedExtensions: allowedExtensions,
                AllowedUserIds: allowedUserIds
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

    function loadFromServer(page) {
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(pluginId).then(config => {
            config = config || {};
            ensureTargetIds(config);
            page.currentConfig = config;
            loadConfiguration(page, config);
            Dashboard.hideLoadingMsg();
        }, () => {
            Dashboard.hideLoadingMsg();
            Dashboard.alert('Failed to load Upload Inbox configuration.');
        });
    }

    function onSubmit(page, e) {
        e.preventDefault();
        const config = page.currentConfig || {};
        readConfigurationFromPage(page, config);

        Dashboard.showLoadingMsg();
        ApiClient.updatePluginConfiguration(pluginId, config).then(() => {
            Dashboard.processPluginConfigurationUpdateResult();
            Dashboard.hideLoadingMsg();
        }, () => {
            Dashboard.hideLoadingMsg();
            Dashboard.alert('Failed to save Upload Inbox configuration.');
        });
    }

    function onAddTargetClick(page) {
        const config = page.currentConfig || { Targets: [] };
        config.Targets = config.Targets || [];
        config.Targets.push({
            Id: (window.crypto && window.crypto.randomUUID) ? window.crypto.randomUUID() : Date.now().toString(),
            DisplayName: '',
            BasePath: '',
            CreateUserSubfolder: true,
            MaxFileSizeBytes: 20 * 1024 * 1024 * 1024,
            AllowedExtensions: [],
            AllowedUserIds: []
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
