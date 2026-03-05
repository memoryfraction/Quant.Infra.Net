/**
 * TokenManager - Client-side JWT token management
 * Handles storage, retrieval, and attachment of JWT tokens
 */

class TokenManager {
    constructor() {
        this.accessToken = null;
        this.refreshToken = null;
        this.expiresIn = null;
        this.tokenCreatedAt = null;
    }

    /**
     * Store tokens from login response
     * @param {Object} response - Login response containing tokens
     */
    storeTokens(response) {
        if (!response) {
            console.error('Invalid response object');
            return;
        }

        this.accessToken = response.accessToken;
        this.refreshToken = response.refreshToken;
        this.expiresIn = response.expiresIn;
        this.tokenCreatedAt = Math.floor(Date.now() / 1000);

        // Store in sessionStorage (not localStorage for security)
        sessionStorage.setItem('accessToken', this.accessToken);
        sessionStorage.setItem('refreshToken', this.refreshToken);
        sessionStorage.setItem('expiresIn', this.expiresIn);
        sessionStorage.setItem('tokenCreatedAt', this.tokenCreatedAt);

        console.log('Tokens stored successfully');
    }

    /**
     * Get the access token
     * @returns {string|null} Access token or null
     */
    getAccessToken() {
        return this.accessToken || sessionStorage.getItem('accessToken');
    }

    /**
     * Get the refresh token
     * @returns {string|null} Refresh token or null
     */
    getRefreshToken() {
        return this.refreshToken || sessionStorage.getItem('refreshToken');
    }

    /**
     * Attach token to request headers
     * @param {Object} headers - Request headers object
     * @returns {Object} Headers with Authorization header added
     */
    attachTokenToRequest(headers = {}) {
        const token = this.getAccessToken();
        if (token) {
            headers['Authorization'] = `Bearer ${token}`;
        }
        return headers;
    }

    /**
     * Check if access token is expired
     * @returns {boolean} True if token is expired
     */
    isAccessTokenExpired() {
        const expiresIn = parseInt(sessionStorage.getItem('expiresIn') || '0');
        const createdAt = parseInt(sessionStorage.getItem('tokenCreatedAt') || '0');

        if (expiresIn === 0 || createdAt === 0) {
            return false;
        }

        const now = Math.floor(Date.now() / 1000);
        const expiresAt = createdAt + expiresIn;

        return now >= expiresAt;
    }

    /**
     * Check if token is about to expire (within 5 minutes)
     * @returns {boolean} True if token is about to expire
     */
    isAccessTokenExpiringSoon() {
        const expiresIn = parseInt(sessionStorage.getItem('expiresIn') || '0');
        const createdAt = parseInt(sessionStorage.getItem('tokenCreatedAt') || '0');

        if (expiresIn === 0 || createdAt === 0) {
            return false;
        }

        const now = Math.floor(Date.now() / 1000);
        const expiresAt = createdAt + expiresIn;
        const fiveMinutesFromNow = now + (5 * 60);

        return now >= (expiresAt - (5 * 60));
    }

    /**
     * Clear all tokens
     */
    clearTokens() {
        this.accessToken = null;
        this.refreshToken = null;
        this.expiresIn = null;
        this.tokenCreatedAt = null;

        sessionStorage.removeItem('accessToken');
        sessionStorage.removeItem('refreshToken');
        sessionStorage.removeItem('expiresIn');
        sessionStorage.removeItem('tokenCreatedAt');

        console.log('Tokens cleared');
    }

    /**
     * Get token expiration time in seconds
     * @returns {number} Seconds until token expires, or 0 if expired
     */
    getTimeUntilExpiration() {
        const expiresIn = parseInt(sessionStorage.getItem('expiresIn') || '0');
        const createdAt = parseInt(sessionStorage.getItem('tokenCreatedAt') || '0');

        if (expiresIn === 0 || createdAt === 0) {
            return 0;
        }

        const now = Math.floor(Date.now() / 1000);
        const expiresAt = createdAt + expiresIn;
        const timeRemaining = expiresAt - now;

        return Math.max(0, timeRemaining);
    }
}

// Create global instance
window.tokenManager = new TokenManager();
