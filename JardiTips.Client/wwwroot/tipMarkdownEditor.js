const editors = new Map();
let nextId = 1;

export function create(element, markdown) {
    const editor = new toastui.Editor({
        el: element,
        height: '360px',
        initialEditType: 'wysiwyg',
        initialValue: markdown || '',
        usageStatistics: false,
        hooks: {
            addImageBlobHook: () => false
        }
    });

    const id = nextId++;
    editors.set(id, editor);
    return id;
}

export function getMarkdown(id) {
    return editors.get(id)?.getMarkdown() ?? '';
}

export function destroy(id) {
    const editor = editors.get(id);
    if (editor) {
        editor.destroy();
        editors.delete(id);
    }
}
