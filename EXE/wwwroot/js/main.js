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
        var dotsEl = document.getElementById("featuredProductsDots");
        var autoplayMs = 3000;
        var autoplayId = null;
        var visibleCount = 1;
        var dotCount = 1;
        var currentIndex = 0;
        var isAnimating = false;
        var pendingResetIndex = null;
        var touchStartX = 0;
        var touchStartY = 0;
        var originalItems = Array.prototype.slice.call(track.querySelectorAll(".nv-featured-strip-item:not([data-featured-clone='true'])"));

        if (originalItems.length === 0) return;

        function getVisibleCount() {
            var width = window.innerWidth || document.documentElement.clientWidth || 0;
            if (width >= 1200) return 4;
            if (width >= 768) return 3;
            if (width >= 480) return 2;
            return 1;
        }

        function getStartForDot(dot) {
            if (originalItems.length <= visibleCount) return 0;
            return Math.min(dot * visibleCount, Math.max(0, originalItems.length - 1));
        }

        function getCurrentDot() {
            return Math.min(dotCount - 1, Math.floor(currentIndex / visibleCount));
        }

        function getStepWidth() {
            var first = track.querySelector(".nv-featured-strip-item");
            if (!first) return 0;
            var rect = first.getBoundingClientRect();
            var style = window.getComputedStyle(track);
            var gap = parseFloat(style.columnGap || style.gap || "0");
            return rect.width + (Number.isFinite(gap) ? gap : 0);
        }

        function setTrackPosition(index, animate) {
            track.style.transition = animate ? "transform .8s ease" : "none";
            track.style.transform = "translateX(" + (-index * getStepWidth()) + "px)";
        }

        function setActiveDot() {
            if (!dotsEl) return;
            var activeDot = getCurrentDot();
            Array.prototype.forEach.call(dotsEl.querySelectorAll(".nv-featured-strip-dot"), function (dot, index) {
                dot.classList.toggle("is-active", index === activeDot);
                dot.setAttribute("aria-current", index === activeDot ? "true" : "false");
            });
        }

        function renderDots() {
            if (!dotsEl) return;
            dotsEl.innerHTML = "";
            if (dotCount <= 1) return;

            for (var i = 0; i < dotCount; i++) {
                var dot = document.createElement("button");
                dot.type = "button";
                dot.className = "nv-featured-strip-dot";
                dot.setAttribute("aria-label", "Chon nhom san pham " + (i + 1));
                (function (dotIndex) {
                    dot.addEventListener("click", function () {
                        goToIndex(getStartForDot(dotIndex), true);
                        startAutoplay();
                    });
                })(i);
                dotsEl.appendChild(dot);
            }
            setActiveDot();
        }

        function rebuildTrack() {
            stopAutoplay();
            track.innerHTML = "";
            visibleCount = getVisibleCount();
            dotCount = Math.max(1, Math.ceil(originalItems.length / visibleCount));
            currentIndex = Math.min(currentIndex, originalItems.length - 1);
            isAnimating = false;
            pendingResetIndex = null;

            var beforeClones = originalItems.slice(-visibleCount).map(function (item) {
                var clone = item.cloneNode(true);
                clone.setAttribute("data-featured-clone", "true");
                return clone;
            });
            var afterClones = originalItems.slice(0, visibleCount).map(function (item) {
                var clone = item.cloneNode(true);
                clone.setAttribute("data-featured-clone", "true");
                return clone;
            });

            beforeClones.concat(originalItems, afterClones).forEach(function (item) {
                track.appendChild(item);
            });

            renderDots();
            requestAnimationFrame(function () {
                setTrackPosition(visibleCount + currentIndex, false);
                startAutoplay();
            });
        }

        function goToIndex(index, animate) {
            if (isAnimating || originalItems.length <= 1) return;

            isAnimating = true;
            pendingResetIndex = null;

            if (index < 0) {
                currentIndex = originalItems.length - 1;
                pendingResetIndex = currentIndex;
                setTrackPosition(visibleCount - 1, animate);
            } else if (index >= originalItems.length) {
                currentIndex = 0;
                pendingResetIndex = currentIndex;
                setTrackPosition(visibleCount + originalItems.length, animate);
            } else {
                currentIndex = index;
                setTrackPosition(visibleCount + currentIndex, animate);
            }

            setActiveDot();

            if (!animate) {
                isAnimating = false;
                if (pendingResetIndex !== null) {
                    setTrackPosition(visibleCount + pendingResetIndex, false);
                    pendingResetIndex = null;
                }
            }
        }

        function nextSlide() {
            goToIndex(currentIndex + 1, true);
        }

        function prevSlide() {
            goToIndex(currentIndex - 1, true);
        }

        function stopAutoplay() {
            if (autoplayId) {
                window.clearInterval(autoplayId);
                autoplayId = null;
            }
        }

        function startAutoplay() {
            stopAutoplay();
            if (originalItems.length <= 1) return;
            autoplayId = window.setInterval(nextSlide, autoplayMs);
        }

        track.addEventListener("transitionend", function (event) {
            if (event.target !== track || event.propertyName !== "transform") return;
            if (pendingResetIndex !== null) {
                setTrackPosition(visibleCount + pendingResetIndex, false);
                pendingResetIndex = null;
            }
            isAnimating = false;
        });

        if (prevBtn) prevBtn.addEventListener("click", function () {
            prevSlide();
            startAutoplay();
        });
        if (nextBtn) nextBtn.addEventListener("click", function () {
            nextSlide();
            startAutoplay();
        });
        if (window.matchMedia("(hover: hover) and (pointer: fine)").matches) {
            viewport.addEventListener("mouseenter", stopAutoplay);
            viewport.addEventListener("mouseleave", startAutoplay);
        }

        viewport.addEventListener("touchstart", function (event) {
            if (!event.touches || event.touches.length === 0) return;
            touchStartX = event.touches[0].clientX;
            touchStartY = event.touches[0].clientY;
        }, { passive: true });

        viewport.addEventListener("touchend", function (event) {
            if (!event.changedTouches || event.changedTouches.length === 0) return;
            var dx = event.changedTouches[0].clientX - touchStartX;
            var dy = event.changedTouches[0].clientY - touchStartY;
            if (Math.abs(dx) > 40 && Math.abs(dx) > Math.abs(dy)) {
                if (dx < 0) nextSlide();
                else prevSlide();
                startAutoplay();
            }
        }, { passive: true });

        window.addEventListener("resize", function () {
            var nextVisibleCount = getVisibleCount();
            if (nextVisibleCount !== visibleCount) {
                currentIndex = 0;
                rebuildTrack();
            } else {
                setTrackPosition(visibleCount + currentIndex, false);
            }
        });

        rebuildTrack();
    }

    $(document).ready(function () {
        initFeaturedStrip();
    });

})(jQuery);
