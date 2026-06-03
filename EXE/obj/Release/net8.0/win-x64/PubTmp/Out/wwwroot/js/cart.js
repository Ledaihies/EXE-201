(function () {
	"use strict";

	function getCookie(name) {
		var v = document.cookie.match('(^|;) ?' + name + '=([^;]*)(;|$)');
		return v ? v[2] : null;
	}

	function fetchWithAntiForgery(url, options) {
		options = options || {};
		options.headers = options.headers || {};
		options.headers["X-Requested-With"] = "XMLHttpRequest";
		var token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || getCookie("XSRF-TOKEN");
		if (token) options.headers["RequestVerificationToken"] = token;
		return fetch(url, options);
	}

	function refreshTotals() {
		fetchWithAntiForgery("/Cart/CartTotals")
			.then(function (r) { return r.json(); })
			.then(function (data) {
				if (data.ok) {
					document.querySelectorAll(".nv-cart-subtotal").forEach(function (el) {
						el.textContent = data.subtotal.toLocaleString("vi-VN") + " đ";
					});
					document.querySelectorAll(".nv-cart-shipping").forEach(function (el) {
						el.textContent = data.shippingFee.toLocaleString("vi-VN") + " đ";
					});
					document.querySelectorAll(".nv-cart-total").forEach(function (el) {
						el.textContent = data.total.toLocaleString("vi-VN") + " đ";
					});
				}
			})
			.catch(function () { });
	}

	function initCart() {
		// Increase quantity
		document.querySelectorAll(".nv-cart-inc").forEach(function (btn) {
			btn.addEventListener("click", function (e) {
				e.preventDefault();
				var href = this.getAttribute("href");
				if (!href) return;
				var card = this.closest(".nv-cart-card");
				var qtyEl = card ? card.querySelector(".nv-cart-qty-val") : null;
				var lineEl = card ? card.querySelector(".nv-cart-line-total") : null;
				fetchWithAntiForgery(href).then(function (r) { return r.json(); }).then(function (data) {
					if (data.ok) {
						if (qtyEl) qtyEl.textContent = data.qty;
						if (lineEl) lineEl.textContent = data.lineTotal.toLocaleString("vi-VN") + " đ";
						refreshTotals();
					} else {
						window.location.href = href;
					}
				}).catch(function () { window.location.href = href; });
			});
		});

		// Decrease quantity
		document.querySelectorAll(".nv-cart-dec").forEach(function (btn) {
			btn.addEventListener("click", function (e) {
				e.preventDefault();
				var href = this.getAttribute("href");
				if (!href) return;
				var card = this.closest(".nv-cart-card");
				var qtyEl = card ? card.querySelector(".nv-cart-qty-val") : null;
				var lineEl = card ? card.querySelector(".nv-cart-line-total") : null;
				fetchWithAntiForgery(href).then(function (r) { return r.json(); }).then(function (data) {
					if (data.ok) {
						if (data.removed && card) {
							card.style.transition = "opacity 0.3s, transform 0.3s";
							card.style.opacity = "0";
							card.style.transform = "scale(0.95)";
							setTimeout(function () {
								card.remove();
								refreshTotals();
								if (!document.querySelector(".nv-cart-card")) {
									window.location.reload();
								}
							}, 300);
						} else {
							if (qtyEl) qtyEl.textContent = data.qty;
							if (lineEl) lineEl.textContent = data.lineTotal.toLocaleString("vi-VN") + " đ";
							refreshTotals();
						}
					} else {
						window.location.href = href;
					}
				}).catch(function () { window.location.href = href; });
			});
		});

		// Remove item - confirm then AJAX
		document.querySelectorAll(".nv-cart-remove").forEach(function (btn) {
			btn.addEventListener("click", function (e) {
				e.preventDefault();
				var id = this.getAttribute("data-id");
				if (!id) return;
				if (!confirm("Bạn có chắc muốn xoá sản phẩm này khỏi giỏ hàng?")) return;
				var card = this.closest(".nv-cart-card");
				fetchWithAntiForgery("/Cart/Remove/" + id, { method: "GET" })
					.then(function (r) { return r.json(); })
					.then(function (data) {
						if (data.ok && card) {
							card.style.transition = "opacity 0.3s, transform 0.3s";
							card.style.opacity = "0";
							card.style.transform = "scale(0.95)";
							setTimeout(function () {
								card.remove();
								refreshTotals();
								if (!document.querySelector(".nv-cart-card")) {
									window.location.reload();
								}
							}, 300);
						} else {
							window.location.href = "/Cart/Remove/" + id;
						}
					})
					.catch(function () { window.location.href = "/Cart/Remove/" + id; });
			});
		});

		// Voucher - placeholder (chưa có API)
		var btnVoucher = document.getElementById("btnApplyVoucher");
		var voucherInput = document.getElementById("voucherCode");
		var voucherMsg = document.getElementById("voucherMessage");
		if (btnVoucher && voucherInput && voucherMsg) {
			btnVoucher.addEventListener("click", function () {
				var code = (voucherInput.value || "").trim();
				if (!code) {
					voucherMsg.textContent = "Vui lòng nhập mã ưu đãi.";
					voucherMsg.className = "small mt-2 text-danger";
					return;
				}
				voucherMsg.textContent = "Mã ưu đãi đang được kiểm tra...";
				voucherMsg.className = "small mt-2 text-muted";
				setTimeout(function () {
					voucherMsg.textContent = "Chức năng mã ưu đãi sẽ sớm được cập nhật.";
					voucherMsg.className = "small mt-2 text-muted";
				}, 800);
			});
		}

		// Lưu để mua sau (Wishlist)
		document.querySelectorAll(".nv-save-for-later").forEach(function (btn) {
			btn.addEventListener("click", function () {
				var productId = this.getAttribute("data-product-id");
				if (!productId) return;
				var icon = this.querySelector("i");
				var token = document.querySelector('input[name="__RequestVerificationToken"]');
				var tokenVal = token ? token.value : "";
				var body = "productId=" + encodeURIComponent(productId) + "&__RequestVerificationToken=" + encodeURIComponent(tokenVal);
				fetch("/Cart/AddToWishlist", {
					method: "POST",
					headers: {
						"Content-Type": "application/x-www-form-urlencoded",
						"X-Requested-With": "XMLHttpRequest"
					},
					body: body
				})
					.then(function (r) { return r.json(); })
					.then(function (data) {
						if (data.ok) {
							if (icon) {
								icon.classList.remove("far");
								icon.classList.add("fas", "text-danger");
							}
							alert(data.message || "Đã lưu!");
						}
					});
			});
		});
	}

	if (document.readyState === "loading") {
		document.addEventListener("DOMContentLoaded", initCart);
	} else {
		initCart();
	}
})();
