// Demizon – GLightbox gallery helper
// Called from Blazor via JS Interop

window.demizonGallery = {
    lightbox: null,

    init: function (items) {
        if (this.lightbox) {
            this.lightbox.destroy();
            this.lightbox = null;
        }
        if (!items || items.length === 0) return;

        this.lightbox = GLightbox({
            elements: items.map(function (item) {
                return {
                    href: item.href,
                    type: 'image',
                    alt: item.alt || ''
                };
            }),
            touchNavigation: true,
            loop: false,
            closeOnOutsideClick: true
        });
    },

    open: function (index) {
        if (this.lightbox) {
            this.lightbox.openAt(index);
        }
    },

    destroy: function () {
        if (this.lightbox) {
            this.lightbox.destroy();
            this.lightbox = null;
        }
    }
};
