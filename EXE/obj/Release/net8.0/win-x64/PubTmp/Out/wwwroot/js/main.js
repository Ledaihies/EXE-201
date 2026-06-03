(function ($) {
    "use strict";

    // Spinner - ẩn nhanh để thanh sản phẩm hiển thị ngay
    var spinner = function () {
        if ($('#spinner').length > 0) {
            $('#spinner').css({ transition: 'opacity 0.12s' }).removeClass('show');
        }
    };
    spinner();

    // Fixed Navbar
    $(window).scroll(function () {
        if ($(window).width() < 992) {
            if ($(this).scrollTop() > 55) {
                $('.fixed-top').addClass('shadow');
            } else {
                $('.fixed-top').removeClass('shadow');
            }
        } else {
            if ($(this).scrollTop() > 55) {
                $('.fixed-top').addClass('shadow').css('top', -55);
            } else {
                $('.fixed-top').removeClass('shadow').css('top', 0);
            }
        }
    });

    // Hero Carousel (THÊM PHẦN NÀY)
    $(".hero-carousel").owlCarousel({
        autoplay: true,
        smartSpeed: 1500,
        items: 1,
        dots: false,
        loop: true,
        nav: true,
        navText: [
            '<i class="fa fa-angle-left"></i>',
            '<i class="fa fa-angle-right"></i>'
        ]
    });

    // Testimonial carousel
    $(".testimonial-carousel").owlCarousel({
        autoplay: true,
        smartSpeed: 2000,
        center: false,
        dots: true,
        loop: true,
        margin: 25,
        nav: true,
        navText: [
            '<i class="fa fa-angle-left"></i>',
            '<i class="fa fa-angle-right"></i>'
        ],
        responsive: {
            0: { items: 1 },
            768: { items: 1 },
            992: { items: 2 }
        }
    });

    // Product carousel
    $(".vegetable-carousel").owlCarousel({
        autoplay: true,
        smartSpeed: 1500,
        center: false,
        dots: true,
        loop: true,
        margin: 25,
        nav: true,
        navText: [
            '<i class="fa fa-angle-left"></i>',
            '<i class="fa fa-angle-right"></i>'
        ],
        responsive: {
            0: { items: 1 },
            768: { items: 2 },
            992: { items: 3 },
            1200: { items: 4 }
        }
    });

    // Hôm nay Nguồn Việt có gì mới – slider 1 hàng, trượt ngang khi bấm mũi tên (không dùng Owl)
    function initFeaturedStrip() {
        var viewport = document.getElementById("featuredProductsViewport");
        var track = document.getElementById("featuredProductsTrack");
        if (!viewport || !track) return;

        var prevBtn = document.querySelector(".nv-featured-strip-arrow--prev");
        var nextBtn = document.querySelector(".nv-featured-strip-arrow--next");

        function getStep() {
            var firstItem = track.querySelector(".nv-featured-strip-item");
            if (firstItem) {
                var style = window.getComputedStyle(firstItem);
                return firstItem.offsetWidth + parseFloat(style.marginRight || "0") + 8;
            }
            return viewport.clientWidth * 0.8;
        }

        var step = getStep();
        window.addEventListener("resize", function () { step = getStep(); });

        function scrollByDelta(delta) {
            viewport.scrollBy({ left: delta, behavior: "smooth" });
        }

        if (prevBtn) prevBtn.addEventListener("click", function () { scrollByDelta(-step); });
        if (nextBtn) nextBtn.addEventListener("click", function () { scrollByDelta(step); });
    }

    $(document).ready(function () {
        initFeaturedStrip();
    });

})(jQuery);