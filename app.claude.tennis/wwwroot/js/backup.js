// Saving and restoring the players backup file. Where the browser can open folders (Chrome and Edge on a computer) the backup
// lives in a social.tennis.app folder in Documents; on iPhone Safari it goes through the share sheet, or a download.
const folderName = 'social.tennis.app';
const databaseName = 'socialTennisBackup';
const storeName = 'handles';
const handleKey = 'folder';

let pickedText = '';

export function supportsFolders() {
    return typeof window.showDirectoryPicker === 'function';
}

// Returns 'folder', 'shared', 'downloaded' or 'cancelled'.
export async function save(fileName, text) {
    if (supportsFolders()) {
        try {
            const folder = await getFolder();
            const handle = await folder.getFileHandle(fileName, { create: true });
            const writable = await handle.createWritable();
            await writable.write(text);
            await writable.close();
            return 'folder';
        } catch (error) {
            if (error?.name === 'AbortError') {
                return 'cancelled';
            }
            // Anything else (no permission, folder removed): fall back to the share sheet or a download.
        }
    }

    const file = new File([text], fileName, { type: 'application/json' });
    if (typeof navigator.share === 'function' && typeof navigator.canShare === 'function' && navigator.canShare({ files: [file] })) {
        try {
            await navigator.share({ files: [file], title: fileName });
            return 'shared';
        } catch (error) {
            if (error?.name === 'AbortError') {
                return 'cancelled';
            }
        }
    }

    const url = URL.createObjectURL(file);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 10000);
    return 'downloaded';
}

// Returns 'ok' (read the text with takeText), 'tooLarge' or 'cancelled'.
export async function pick(maxLength) {
    let file;
    try {
        file = supportsFolders() ? await pickFromFolder() : await pickWithInput();
    } catch (error) {
        if (error?.name === 'AbortError') {
            return 'cancelled';
        }
        throw error;
    }

    if (file === null) {
        return 'cancelled';
    }

    if (file.size > maxLength) {
        return 'tooLarge';
    }

    pickedText = await file.text();
    return 'ok';
}

export function takeText() {
    const text = pickedText;
    pickedText = '';
    return text;
}

async function pickFromFolder() {
    const folder = await loadHandle();
    const options = {
        types: [{ description: 'Players backup', accept: { 'application/json': ['.json'] } }],
        startIn: folder ?? 'documents',
    };
    const [handle] = await window.showOpenFilePicker(options);
    return await handle.getFile();
}

function pickWithInput() {
    return new Promise(resolve => {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = '.json,application/json';
        input.addEventListener('change', () => resolve(input.files[0] ?? null));
        input.addEventListener('cancel', () => resolve(null));
        input.click();
    });
}

async function getFolder() {
    const stored = await loadHandle();
    if (stored) {
        let permission = await stored.queryPermission({ mode: 'readwrite' });
        if (permission !== 'granted') {
            permission = await stored.requestPermission({ mode: 'readwrite' });
        }

        if (permission === 'granted') {
            return stored;
        }
    }

    // The first time: choose Documents, and the folder is created there.
    const picked = await window.showDirectoryPicker({ id: 'social-tennis-backup', mode: 'readwrite', startIn: 'documents' });
    const folder = picked.name === folderName ? picked : await picked.getDirectoryHandle(folderName, { create: true });
    await storeHandle(folder);
    return folder;
}

function openDatabase() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName, 1);
        request.onupgradeneeded = () => request.result.createObjectStore(storeName);
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

async function loadHandle() {
    try {
        const database = await openDatabase();
        return await new Promise(resolve => {
            const request = database.transaction(storeName).objectStore(storeName).get(handleKey);
            request.onsuccess = () => resolve(request.result ?? null);
            request.onerror = () => resolve(null);
        });
    } catch {
        return null;
    }
}

async function storeHandle(handle) {
    try {
        const database = await openDatabase();
        await new Promise(resolve => {
            const transaction = database.transaction(storeName, 'readwrite');
            transaction.objectStore(storeName).put(handle, handleKey);
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => resolve();
        });
    } catch {
        // Remembering the folder is a convenience; the backup was still saved.
    }
}
