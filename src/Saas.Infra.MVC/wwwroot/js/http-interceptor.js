/**
 * HTTP Interceptor - Attaches JWT tokens to API requests
 * Intercepts all fetch requests and adds Authorization header
 */

class HttpInterceptor {
    constructor() {
        this.setupFetchInterceptor();
    }

    /**
     * Setup fetch interceptor to attach tokens to all requests
     */
    setupFetchInterceptor() {
        const originalFetch = window.fetch;

        window.fetch = async function(...args) {
            const [resource, config] = args;
            const url = typeof resource === 'string' ? resource : resource.url;

            // Only attach token to API requests (not static assets)
            if (this.isApiRequest(url)) {
                const headers = config?.headers || {};
                
                // Attach token using TokenManager
                if (window.tokenManager) {
                    window.tokenManager.attachTokenToRequest(headers);
                }

                // Update config with new headers
                const newConfig = {
                    ...config,
                    headers: headers
                };

                return originalFetch.call(this, resource, newConfig);
            }

            return originalFetch.apply(this, args);
        }.bind(this);
    }

    /**
     * Check if URL is an API request
     * @param {string} url - URL to check
     * @returns {boolean} True if URL is an API request
     */
    isApiRequest(url) {
        // Check if URL contains /api/ or /sso/
        return url.includes('/api/') || url.includes('/sso/');
    }

    /**
     * Make an API request with token attachment
     * @param {string} url - API endpoint URL
     * @param {Object} options - Fetch options
     * @returns {Promise} Fetch promise
     */
    async request(url, options = {}) {
        const headers = options.headers || {};
        
        // Attach token
        if (window.tokenManager) {
            window.tokenManager.attachTokenToRequest(headers);
        }

        const config = {
            ...options,
            headers: headers
        };

        return fetch(url, config);
    }

    /**
     * Make a GET request
     * @param {string} url - API endpoint URL
     * @returns {Promise} Fetch promise
     */
    async get(url) {
        return this.request(url, { method: 'GET' });
    }

    /**
     * Make a POST request
     * @param {string} url - API endpoint URL
     * @param {Object} data - Request body
     * @returns {Promise} Fetch promise
     */
    async post(url, data) {
        return this.request(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });
    }

    /**
     * Make a PUT request
     * @param {string} url - API endpoint URL
     * @param {Object} data - Request body
     * @returns {Promise} Fetch promise
     */
    async put(url, data) {
        return this.request(url, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });
    }

    /**
     * Make a DELETE request
     * @param {string} url - API endpoint URL
     * @returns {Promise} Fetch promise
     */
    async delete(url) {
        return this.request(url, { method: 'DELETE' });
    }
}

// Initialize HTTP interceptor when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    window.httpInterceptor = new HttpInterceptor();
});
