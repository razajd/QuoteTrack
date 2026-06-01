window.quoteTrackUi = {
    saveDashboardOrder: function (key, order) {
        try {
            localStorage.setItem(key, JSON.stringify(order || []));
            return true;
        } catch {
            return false;
        }
    },
    loadDashboardOrder: function (key) {
        try {
            const raw = localStorage.getItem(key);
            if (!raw) return [];
            const parsed = JSON.parse(raw);
            return Array.isArray(parsed) ? parsed : [];
        } catch {
            return [];
        }
    },
    clearDashboardOrder: function (key) {
        try {
            localStorage.removeItem(key);
            return true;
        } catch {
            return false;
        }
    }
};
