const databaseName = "JardiTips";
const databaseVersion = 4;
const storeNames = ["categorySnapshots", "tipSnapshots", "authenticationSessions"];

let databasePromise;

function openDatabase() {
    databasePromise ??= new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName, databaseVersion);

        request.onupgradeneeded = () => {
            const database = request.result;
            for (const storeName of storeNames) {
                if (!database.objectStoreNames.contains(storeName)) {
                    database.createObjectStore(storeName);
                }
            }
        };

        request.onsuccess = () => {
            const database = request.result;
            database.onversionchange = () => {
                database.close();
                databasePromise = undefined;
            };
            resolve(database);
        };
        request.onerror = () => {
            databasePromise = undefined;
            reject(request.error);
        };
        request.onblocked = () => {
            databasePromise = undefined;
            reject(new Error(`Opening IndexedDB database '${databaseName}' was blocked.`));
        };
    });

    return databasePromise;
}

function ensureStore(database, storeName) {
    if (!database.objectStoreNames.contains(storeName)) {
        throw new Error(`IndexedDB store '${storeName}' is not configured.`);
    }
}

export async function get(storeName, key) {
    const database = await openDatabase();
    ensureStore(database, storeName);

    return new Promise((resolve, reject) => {
        const transaction = database.transaction(storeName, "readonly");
        const request = transaction.objectStore(storeName).get(key);

        request.onsuccess = () => resolve(request.result ?? null);
        request.onerror = () => reject(request.error);
    });
}

export async function remove(storeName, key) {
    const database = await openDatabase();
    ensureStore(database, storeName);

    return new Promise((resolve, reject) => {
        const transaction = database.transaction(storeName, "readwrite");
        transaction.objectStore(storeName).delete(key);

        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error);
        transaction.onabort = () => reject(transaction.error);
    });
}

export async function clearDatabase() {
    if (databasePromise) {
        try {
            const database = await databasePromise;
            database.close();
        } catch {
        }

        databasePromise = undefined;
    }

    return new Promise((resolve, reject) => {
        const request = indexedDB.deleteDatabase(databaseName);

        request.onsuccess = () => resolve();
        request.onerror = () => reject(request.error);
        request.onblocked = () => reject(new Error(`Deleting IndexedDB database '${databaseName}' was blocked.`));
    });
}

export async function replace(storeName, key, value) {
    const database = await openDatabase();
    ensureStore(database, storeName);

    return new Promise((resolve, reject) => {
        const transaction = database.transaction(storeName, "readwrite");
        const store = transaction.objectStore(storeName);

        store.clear();
        store.put(value, key);

        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error);
        transaction.onabort = () => reject(transaction.error);
    });
}

export async function put(storeName, key, value) {
    const database = await openDatabase();
    ensureStore(database, storeName);

    return new Promise((resolve, reject) => {
        const transaction = database.transaction(storeName, "readwrite");
        transaction.objectStore(storeName).put(value, key);

        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error);
        transaction.onabort = () => reject(transaction.error);
    });
}
