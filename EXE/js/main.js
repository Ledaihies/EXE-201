(function ($) {
    "use strict";

    // Spinner
    var spinner = function () {
        setTimeout(function () {
            if ($('#spinner').length > 0) {
                $('#spinner').removeClass('show');
            }
        }, 1);
    };
    spinner(0);


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
    
    
   // Back to top button
   $(window).scroll(function () {
    if ($(this).scrollTop() > 300) {
        $('.back-to-top').fadeIn('slow');
    } else {
        $('.back-to-top').fadeOut('slow');
    }
    });
    $('.back-to-top').click(function () {
        $('html, body').animate({scrollTop: 0}, 1500, 'easeInOutExpo');
        return false;
    });


    // vegetable carousel
    $(".vegetable-carousel").owlCarousel({
        autoplay: true,
        smartSpeed: 1500,
        center: false,
        dots: true,
        loop: true,
        margin: 25,
        nav : true,
        navText : [
            '<i class="bi bi-arrow-left"></i>',
            '<i class="bi bi-arrow-right"></i>'
        ],
        responsiveClass: true,
        responsive: {
            0:{
                items:1
            },
            576:{
                items:1
            },
            768:{
                items:2
            },
            992:{
                items:3
            },
            1200:{
                items:4
            }
        }
    });

    // featured fruits carousel (Đặc sản nổi bật)
    $(".fruite-carousel").owlCarousel({
        autoplay: true,
        smartSpeed: 1500,
        center: false,
        dots: false,
        loop: true,
        margin: 25,
        nav: true,
        navText: [
            '<i class="bi bi-arrow-left"></i>',
            '<i class="bi bi-arrow-right"></i>'
        ],
        responsiveClass: true,
        responsive: {
            0: {
                items: 1
            },
            576: {
                items: 2
            },
            768: {
                items: 3
            },
            992: {
                items: 4
            },
            1200: {
                items: 5
            }
        }
    });


    // Modal Video
    $(document).ready(function () {
        var $videoSrc;
        $('.btn-play').click(function () {
            $videoSrc = $(this).data("src");
        });
        console.log($videoSrc);

        $('#videoModal').on('shown.bs.modal', function (e) {
            $("#video").attr('src', $videoSrc + "?autoplay=1&amp;modestbranding=1&amp;showinfo=0");
        })

        $('#videoModal').on('hide.bs.modal', function (e) {
            $("#video").attr('src', $videoSrc);
        })
    });


    // Region filter (Đặc sản vùng miền)
    // Usage:
    // - Product card: any element with [data-region] and optional [data-region-label]
    // - Filter button: any element with [data-region-filter] ("all" or region key)
    var initRegionFilter = function () {
        var $filterButtons = $('[data-region-filter]');
        var $items = $('[data-region]');

        if ($filterButtons.length === 0 || $items.length === 0) return;

        var setActive = function (region) {
            $filterButtons.removeClass('active');
            $filterButtons.filter('[data-region-filter="' + region + '"]').addClass('active');
        };

        var apply = function (region) {
            if (!region || region === 'all') {
                $items.closest('.region-item, .col-md-6, .col-lg-6, .col-xl-4, .col-xl-3, .col-lg-4, .col-lg-3').show();
                $items.show();
                return;
            }

            $items.each(function () {
                var $el = $(this);
                var regions = ($el.data('region') || '').toString().split(',').map(function (s) { return s.trim(); });
                var match = regions.indexOf(region) !== -1;

                // hide the whole card column if possible
                var $container = $el.closest('.region-item, .col-md-6, .col-lg-6, .col-xl-4, .col-xl-3, .col-lg-4, .col-lg-3');
                if ($container.length) {
                    $container.toggle(match);
                } else {
                    $el.toggle(match);
                }
            });
        };

        $filterButtons.off('click.regionFilter').on('click.regionFilter', function (e) {
            e.preventDefault();
            var region = ($(this).data('region-filter') || 'all').toString();
            setActive(region);
            apply(region);
        });

        // Default active: all (or the one marked with .active)
        var $preActive = $filterButtons.filter('.active').first();
        var initial = $preActive.length ? ($preActive.data('region-filter') || 'all') : 'all';
        setActive(initial);
        apply(initial);
    };

    $(document).ready(function () {
        initRegionFilter();
    });

})(jQuery);

