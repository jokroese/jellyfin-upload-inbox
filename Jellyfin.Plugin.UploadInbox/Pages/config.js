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

    function createFieldContainer(parent) {
        const c = document.createElement('div');
        c.className = 'inputContainer';
        parent.appendChild(c);
        return c;
    }

    function createTargetRow(target, index) {
        const row = document.createElement('div');
        row.className = 'uploadTargetRow';

        const fields = document.createElement('div');
        fields.className = 'uploadTargetFields';
        row.appendChild(fields);

        // Display name
        {
            const c = createFieldContainer(fields);
            appendLabel(c, 'Display name');
            appendTextInput(c, 'txtDisplayName', target.DisplayName);
        }

        // Base path
        {
            const c = createFieldContainer(fields);
            appendLabel(c, 'Base path (on server)');
            appendTextInput(c, 'txtBasePath', target.BasePath);
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

        // Create per-user subfolder
        {
            const c = createFieldContainer(fields);
            appendLabel(c, 'Create per-user subfolder');
            appendCheckbox(c, 'chkUserSubfolder', target.CreateUserSubfolder);
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
            container.appendChild(createTargetRow(t, i));
        });
    }

    function readConfigurationFromPage(page, config) {
        const targets = [];
        const container = getTargetsContainer(page);
        const rows = container.querySelectorAll('.uploadTargetRow');

        rows.forEach((row, index) => {
            const id = (config.Targets && config.Targets[index] && config.Targets[index].Id) || null;
            const displayName = row.querySelector('.txtDisplayName').value || '';
            const basePath = row.querySelector('.txtBasePath').value || '';
            const accessMode = row.querySelector('.selAccessMode')?.value || 'AllUsers';
            const createUserSubfolder = row.querySelector('.chkUserSubfolder').checked;
            const maxGb = parseInt(row.querySelector('.txtMaxSize').value || '20', 10);
            const maxBytes = maxGb * 1024 * 1024 * 1024;
            const exts = row.querySelector('.txtExtensions').value || '';

            const allowedExtensions = exts
                .split(',')
                .map(e => e.trim())
                .filter(e => e.length > 0);

            targets.push({
                Id: id || null,
                DisplayName: displayName,
                BasePath: basePath,
                AccessMode: accessMode,
                CreateUserSubfolder: createUserSubfolder,
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
            AccessMode: 'AllUsers',
            CreateUserSubfolder: true,
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
