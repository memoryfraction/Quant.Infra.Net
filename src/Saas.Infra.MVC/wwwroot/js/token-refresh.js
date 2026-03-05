/**
 * Token Refresh Manager - Handles JWT token refresh logic
 * Automatically refreshes tokens when they expire or are about to expire
 */

class TokenRefreshManager {
    constructor(refreshEndpoint = '/api/auth/refresh') {
        this.refreshEndpoint = refreshEndpoint;
        this.refreshTimer = null;
        this.isRefreshing = false;
        this.refreshPromise = null;
        this.init();
    }

    /**
     * Initialize token refresh manager
     */
    init() {
        // Check if token exists and schedule refresh
        if (window.tokenManager && window.tokenManager.getAccessToken()) {
            this.scheduleTokenRefresh();
        }

        // Listen for storage changes (token updates from other tabs)
        window.addEventListener('storage', (e) => {
            if (e.key === 'accessToken' || e.key === 'expiresIn') {
                this.scheduleTokenRefresh();
            }
        });
    }

    /**
     * Schedule token refresh based on expiration time
     */
    scheduleTokenRefresh() {
        // Clear existing timer
        if (this.refreshTimer) {
            clearTimeout(this.refreshTimer);
        }

        if (!window.tokenManager) {
            return;
        }

        const timeUntilExpiration = window.tokenManager.getTimeUntilExpiration();
        
        if (timeUntilExpiration <= 0) {
            // Token already expired
            this.refreshToken();
            return;
        }

        // Refresh token 5 minutes before expiration
        const refreshTime = Math.max(0, (timeUntilExpiration - 300) * 1000);

        this.refreshTimer = setTimeout(() => {
            this.refreshToken();
        }, refreshTime);

        console.log(`Token refresh scheduled in ${Math.floor(refreshTime / 1000)} seconds`);
    }

    /**
     * Refresh the access token using refresh token
     * @returns {Promise} Promise that resolves when token is refreshed
     */
    async refreshToken() {
        // Prevent multiple simultaneous refresh requests
        if (this.isRefreshing) {
            return this.refreshPromise;
        }

        this.isRefreshing = true;

        this.refreshPromise = (async () => {
            try {
                const refreshToken = window.tokenManager?.getRefreshToken();

                if (!refreshToken) {
                    console.warn('No refresh token available');
                    this.handleRefreshFailure();
                    return false;
                }

                console.log('Refreshing access token...');

                const response = await fetch(this.refreshEndpoint, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({
                        refreshToken: refreshToken
                    })
                });

                if (!response.ok) {
                    console.error('Token refresh failed:', response.status);
                    this.handleRefreshFailure();
                    return false;
                }

                const data = await response.json();

                // Update tokens
                if (data.accessToken && data.refreshToken) {
                    window.tokenManager.storeTokens({
                        accessToken: data.accessToken,
                        refreshToken: data.refreshToken,
                        expiresIn: data.expiresIn
                    });

                    console.log('Token refreshed successfully');

                    // Schedule next refresh
                    this.scheduleTokenRefresh();

                    return true;
                } else {
                    console.error('Invalid token refresh response');
                    this.handleRefreshFailure();
                    return false;
                }
            } catch (error) {
                console.error('Token refresh error:', error);
                this.handleRefreshFailure();
                return false;
            } finally {
                this.isRefreshing = false;
            }
        })();

        return this.refreshPromise;
    }

    /**
     * Handle token refresh failure
     */
    handleRefreshFailure() {
        console.warn('Token refresh failed, clearing tokens and redirecting to login');
        
        // Clear tokens
        if (window.tokenManager) {
            window.tokenManager.clearTokens();
        }

        // Redirect to login page
        window.location.href = '/account/login';
    }

    /**
     * Retry a failed request after token refresh
     * @param {string} url - Request URL
     * @param {Object} options - Fetch options
     * @returns {Promise} Fetch promise
     */
    async retryRequestWithRefresh(url, options = {}) {
        // Refresh token first
        const refreshed = await this.refreshToken();

        if (!refreshed) {
            throw new Error('Token refresh failed');
        }

        // Retry original request with new token
        const headers = options.headers || {};
        if (window.tokenManager) {
            window.tokenManager.attachTokenToRequest(headers);
        }

        const newOptions = {
            ...options,
            headers: headers
        };

        return fetch(url, newOptions);
    }

    /**
     * Setup response interceptor to handle 401 responses
     */
    setupResponseInterceptor() {
        const originalFetch = window.fetch;

        window.fetch = async function(...args) {
            let response = await originalFetch.apply(this, args);

            // If 401 Unauthorized, try to refresh token and retry
            if (response.status === 401) {
                console.log('Received 401 Unauthorized, attempting token refresh...');

                const [resource, config] = args;
                const refreshed = await window.tokenRefreshManager?.refreshToken();

                if (refreshed) {
                    // Retry request with new token
                    response = await originalFetch.apply(this, args);
                }
            }

            return response;
        };
    }
}

// Initialize token refresh manager when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    window.tokenRefreshManager = new TokenRefreshManager('/api/auth/refresh');
    window.tokenRefreshManager.setupResponseInterceptor();
});
