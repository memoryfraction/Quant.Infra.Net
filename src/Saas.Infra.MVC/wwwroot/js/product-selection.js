/**
 * Product Selection Handler
 * Manages product selection and navigation with JWT token attachment
 */

class ProductSelectionHandler {
    constructor() {
        this.accessToken = null;
        this.refreshToken = null;
        this.expiresIn = null;
        this.init();
    }

    /**
     * Initialize the product selection handler
     */
    init() {
        // Retrieve tokens from sessionStorage
        this.accessToken = sessionStorage.getItem('accessToken');
        this.refreshToken = sessionStorage.getItem('refreshToken');
        this.expiresIn = parseInt(sessionStorage.getItem('expiresIn') || '0');

        // Attach click handlers to product buttons
        this.attachProductButtonHandlers();
    }

    /**
     * Attach click handlers to all product buttons
     */
    attachProductButtonHandlers() {
        const productButtons = document.querySelectorAll('.product-button');
        productButtons.forEach(button => {
            button.addEventListener('click', (e) => this.handleProductSelection(e));
        });
    }

    /**
     * Handle product selection
     * @param {Event} event - Click event
     */
    handleProductSelection(event) {
        event.preventDefault();
        
        const button = event.target;
        const productUrl = button.getAttribute('data-product-url');
        const productId = button.getAttribute('data-product-id');

        if (!productUrl) {
            console.error('Product URL not found');
            return;
        }

        // Log product selection
        console.log(`Navigating to product: ${productId} (${productUrl})`);

        // Navigate to product URL
        // The JWT token will be attached by the HTTP interceptor
        window.location.href = productUrl;
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
     * Check if token is expired
     * @returns {boolean} True if token is expired
     */
    isTokenExpired() {
        if (!this.expiresIn) {
            return false;
        }

        // Get token creation time from sessionStorage
        const createdAt = parseInt(sessionStorage.getItem('tokenCreatedAt') || '0');
        if (createdAt === 0) {
            return false;
        }

        const now = Math.floor(Date.now() / 1000);
        const expiresAt = createdAt + this.expiresIn;

        return now >= expiresAt;
    }

    /**
     * Clear tokens
     */
    clearTokens() {
        this.accessToken = null;
        this.refreshToken = null;
        this.expiresIn = null;
        sessionStorage.removeItem('accessToken');
        sessionStorage.removeItem('refreshToken');
        sessionStorage.removeItem('expiresIn');
        sessionStorage.removeItem('tokenCreatedAt');
    }
}

// Initialize product selection handler when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    window.productSelectionHandler = new ProductSelectionHandler();
});
