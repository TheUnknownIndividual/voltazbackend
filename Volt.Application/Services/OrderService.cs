using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Volt.Application.Dtos;
using Volt.Application.Dtos.Order;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class OrderService : IOrderService
    {
        private static readonly Regex AzerbaijanPhoneRegex = new(@"^(\+994\d{9}|0\d{9})$", RegexOptions.Compiled);
        private readonly IUnitOfWork _uow;
        private readonly IOrderEmailService _emailService;

        public OrderService(IUnitOfWork uow, IOrderEmailService emailService)
        {
            _uow = uow;
            _emailService = emailService;
        }

        public async Task<ApiResponse<IReadOnlyList<OrderDto>>> GetAllAsync(byte? status = null, CancellationToken ct = default)
        {
            var orders = await _uow.Repository<Order>().ListNoTrackingAsync(x => x.IsActive, ct);

            if (status is not null)
            {
                orders = orders.Where(x => x.Status == status.Value).ToList();
            }

            var result = new List<OrderDto>();
            foreach (var order in orders.OrderByDescending(x => x.CreatedAt))
            {
                result.Add(await MapToDtoAsync(order, ct));
            }

            return ApiResponse<IReadOnlyList<OrderDto>>.SuccessResponse(result);
        }

        public async Task<ApiResponse<IReadOnlyList<OrderDto>>> GetByCustomerEmailAsync(string email, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return ApiResponse<IReadOnlyList<OrderDto>>.ErrorResponse(ErrorCode.INVALID_ORDER_REQUEST, ErrorCode.INVALID_ORDER_REQUEST);
            }

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var orders = await _uow.Repository<Order>().ListNoTrackingAsync(
                x => x.IsActive && x.Email.ToLower() == normalizedEmail,
                ct);

            var result = new List<OrderDto>();
            foreach (var order in orders.OrderByDescending(x => x.CreatedAt))
            {
                result.Add(await MapToDtoAsync(order, ct));
            }

            return ApiResponse<IReadOnlyList<OrderDto>>.SuccessResponse(result);
        }

        public async Task<ApiResponse<OrderDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var order = await _uow.Repository<Order>()
                .FirstOrDefaultNoTrackingAsync(x => x.Id == id && x.IsActive, ct);

            if (order is null)
            {
                return ApiResponse<OrderDto>.ErrorResponse(ErrorCode.ORDER_NOT_FOUND, ErrorCode.ORDER_NOT_FOUND);
            }

            return ApiResponse<OrderDto>.SuccessResponse(await MapToDtoAsync(order, ct));
        }

        public async Task<ApiResponse<OrderDto>> LookupAsync(string orderNumber, string email, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(orderNumber) || string.IsNullOrWhiteSpace(email))
            {
                return ApiResponse<OrderDto>.ErrorResponse(ErrorCode.INVALID_ORDER_REQUEST, ErrorCode.INVALID_ORDER_REQUEST);
            }

            var normalizedOrderNumber = orderNumber.Trim();
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var order = await _uow.Repository<Order>().FirstOrDefaultNoTrackingAsync(
                x => x.IsActive && x.OrderNumber == normalizedOrderNumber && x.Email.ToLower() == normalizedEmail,
                ct);

            if (order is null)
            {
                return ApiResponse<OrderDto>.ErrorResponse(ErrorCode.ORDER_NOT_FOUND, ErrorCode.ORDER_NOT_FOUND);
            }

            return ApiResponse<OrderDto>.SuccessResponse(await MapToDtoAsync(order, ct));
        }

        public async Task<ApiResponse<OrderDto>> CreateAsync(OrderCreateRequest request, CancellationToken ct = default)
        {
            var validationError = ValidateCreateRequest(request);
            if (validationError is not null)
            {
                return ApiResponse<OrderDto>.ErrorResponse(ErrorCode.INVALID_ORDER_REQUEST, validationError);
            }

            try
            {
                var orderItems = new List<OrderItem>();
                var requiresManualConfirmation = false;
                var requiresInventoryOrPriceConfirmation = false;
                decimal productsSubtotal = 0;
                var requestIntent = ResolveRequestedIntent(request);

                foreach (var requestItem in request.Items)
                {
                    var product = await _uow.Repository<Product>()
                        .FirstOrDefaultAsync(x => x.Id == requestItem.ProductId && x.IsActive, ct);

                    if (product is null)
                    {
                        return ApiResponse<OrderDto>.ErrorResponse(ErrorCode.PRODUCT_NOT_FOUND, ErrorCode.PRODUCT_NOT_FOUND);
                    }

                    var parameters = await _uow.Repository<ProductParametr>()
                        .ListNoTrackingAsync(x => x.ProductId == product.Id && x.IsActive, ct);

                    var selectedPower = NormalizeNullable(requestItem.SelectedPower);
                    ProductParametr selectedParameter = null;

                    if (!string.IsNullOrWhiteSpace(selectedPower))
                    {
                        selectedParameter = parameters.FirstOrDefault(x =>
                            string.Equals(NormalizeNullable(x.TechnicalPower), selectedPower, StringComparison.OrdinalIgnoreCase));

                        if (selectedParameter is null)
                        {
                            requiresManualConfirmation = true;
                            requiresInventoryOrPriceConfirmation = true;
                        }
                    }

                    selectedParameter ??= parameters.FirstOrDefault();

                    var unitPrice = selectedParameter?.Amount ?? 0;
                    var hasStock = product.InStock && selectedParameter?.Count.GetValueOrDefault() >= requestItem.Quantity;
                    var canReserve = requestIntent == (byte)OrderIntent.Purchase && unitPrice > 0 && selectedParameter is not null && hasStock;

                    if (unitPrice <= 0 || selectedParameter is null || !hasStock)
                    {
                        requiresManualConfirmation = true;
                        requiresInventoryOrPriceConfirmation = true;
                    }

                    var image = await _uow.Repository<ProductImage>()
                        .FirstOrDefaultNoTrackingAsync(x => x.ProductId == product.Id && x.Type, ct);

                    var lineTotal = unitPrice * requestItem.Quantity;
                    productsSubtotal += lineTotal;

                    orderItems.Add(new OrderItem
                    {
                        ProductId = product.Id,
                        ProductName = product.ProductName,
                        ProductImageUrl = image?.ImageUrl ?? string.Empty,
                        ProductParametrId = selectedParameter?.Id,
                        SelectedPower = selectedParameter?.TechnicalPower ?? selectedPower ?? string.Empty,
                        Quantity = requestItem.Quantity,
                        ReservedQuantity = canReserve ? requestItem.Quantity : 0,
                        InventoryReleased = false,
                        UnitPrice = unitPrice,
                        LineTotal = lineTotal
                    });

                    if (canReserve)
                    {
                        var previousInStock = product.InStock;
                        selectedParameter.Count = selectedParameter.Count.GetValueOrDefault() - requestItem.Quantity;
                        _uow.Repository<ProductParametr>().Update(selectedParameter);
                        product.InStock = parameters.Any(x => x.Count.GetValueOrDefault() > 0);
                        if (!product.InStock && previousInStock)
                        {
                            product.OutOfStockAt = DateTime.UtcNow;
                        }
                        if (product.InStock)
                        {
                            product.OutOfStockAt = null;
                        }
                        _uow.Repository<Product>().Update(product);
                    }
                }

                var deliveryFee = CalculateDeliveryFee(request.Delivery.Method, ref requiresManualConfirmation);
                var discountTotal = 0m;
                var finalTotal = productsSubtotal + (deliveryFee ?? 0) - discountTotal;
                var now = DateTime.UtcNow;
                if (requestIntent == (byte)OrderIntent.Purchase && requiresInventoryOrPriceConfirmation)
                {
                    requestIntent = orderItems.Any(x => x.UnitPrice <= 0)
                        ? (byte)OrderIntent.PriceQuote
                        : (byte)OrderIntent.OutOfStockContact;
                }

                var order = new Order
                {
                    OrderNumber = "PENDING",
                    Status = (byte)OrderStatus.New,
                    PaymentStatus = ResolveInitialPaymentStatus(request.PaymentMethod),
                    PaymentMethod = request.PaymentMethod,
                    Source = request.Source,
                    Intent = requestIntent,
                    RequiresManualConfirmation = requiresManualConfirmation,
                    AcceptedTerms = request.AcceptedTerms,
                    TermsAcceptedAt = request.AcceptedTerms ? now : null,
                    FullName = request.Contact.FullName.Trim(),
                    Phone = NormalizePhone(request.Contact.Phone),
                    Email = request.Contact.Email.Trim(),
                    DeliveryMethod = request.Delivery.Method,
                    CityOrRegion = NormalizeNullable(request.Delivery.CityOrRegion) ?? string.Empty,
                    District = NormalizeNullable(request.Delivery.District) ?? string.Empty,
                    StreetAndBuilding = NormalizeNullable(request.Delivery.StreetAndBuilding) ?? string.Empty,
                    ApartmentOrOffice = NormalizeNullable(request.Delivery.ApartmentOrOffice) ?? string.Empty,
                    DeliveryNotes = NormalizeNullable(request.Delivery.DeliveryNotes) ?? string.Empty,
                    PickupLocation = NormalizeNullable(request.Delivery.PickupLocation) ?? string.Empty,
                    ProductsSubtotal = productsSubtotal,
                    DeliveryFee = deliveryFee,
                    DiscountTotal = discountTotal,
                    FinalTotal = finalTotal < 0 ? 0 : finalTotal,
                    CreatedAt = now,
                    IsActive = true,
                    Items = orderItems
                };

                await _uow.Repository<Order>().AddAsync(order, ct);
                await _uow.SaveChangesAsync(ct);

                order.OrderNumber = $"VOLT-{now:yyyyMMdd}-{order.Id:D6}";
                order.UpdatedAt = DateTime.UtcNow;
                _uow.Repository<Order>().Update(order);
                await _uow.SaveChangesAsync(ct);

                var dto = await MapToDtoAsync(order, ct);
                try
                {
                    await _emailService.SendOrderConfirmationAsync(dto, ct);
                }
                catch
                {
                    // Order persistence is the source of truth. Email failures are logged by the email service.
                }

                return ApiResponse<OrderDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<OrderDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while creating the order.");
            }
        }

        public async Task<ApiResponse<OrderDto>> UpdateStatusAsync(int id, OrderStatusUpdateRequest request, CancellationToken ct = default)
        {
            if (!Enum.IsDefined(typeof(OrderStatus), request.Status))
            {
                return ApiResponse<OrderDto>.ErrorResponse(ErrorCode.INVALID_ORDER_STATUS, ErrorCode.INVALID_ORDER_STATUS);
            }

            if (request.PaymentStatus is not null && !Enum.IsDefined(typeof(OrderPaymentStatus), request.PaymentStatus.Value))
            {
                return ApiResponse<OrderDto>.ErrorResponse(ErrorCode.INVALID_PAYMENT_STATUS, ErrorCode.INVALID_PAYMENT_STATUS);
            }

            var repo = _uow.Repository<Order>();
            var order = await repo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
            if (order is null)
            {
                return ApiResponse<OrderDto>.ErrorResponse(ErrorCode.ORDER_NOT_FOUND, ErrorCode.ORDER_NOT_FOUND);
            }

            try
            {
                var previousStatus = order.Status;
                var items = await _uow.Repository<OrderItem>().ListNoTrackingAsync(x => x.OrderId == order.Id, ct);

                if (request.Status == (byte)OrderStatus.Cancelled && previousStatus != (byte)OrderStatus.Cancelled)
                {
                    await ReleaseReservedInventoryAsync(items, ct);
                }

                if (previousStatus == (byte)OrderStatus.Cancelled && request.Status != (byte)OrderStatus.Cancelled)
                {
                    var reserveError = await TryReserveInventoryAsync(items, ct);
                    if (reserveError is not null)
                    {
                        return ApiResponse<OrderDto>.ErrorResponse(ErrorCode.INVALID_ORDER_REQUEST, reserveError);
                    }
                }

                order.Status = request.Status;
                if (request.PaymentStatus is not null)
                {
                    order.PaymentStatus = request.PaymentStatus.Value;
                }

                order.UpdatedAt = DateTime.UtcNow;
                repo.Update(order);
                await _uow.SaveChangesAsync(ct);

                return ApiResponse<OrderDto>.SuccessResponse(await MapToDtoAsync(order, ct));
            }
            catch
            {
                return ApiResponse<OrderDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while updating order status.");
            }
        }

        public async Task<ApiResponse<OrderDto>> MarkViewedAsync(int id, CancellationToken ct = default)
        {
            var repo = _uow.Repository<Order>();
            var order = await repo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
            if (order is null)
            {
                return ApiResponse<OrderDto>.ErrorResponse(ErrorCode.ORDER_NOT_FOUND, ErrorCode.ORDER_NOT_FOUND);
            }

            if (!order.IsViewedByAdmin)
            {
                order.IsViewedByAdmin = true;
                order.AdminViewedAt = DateTime.UtcNow;
                order.UpdatedAt = order.AdminViewedAt;
                repo.Update(order);
                await _uow.SaveChangesAsync(ct);
            }

            return ApiResponse<OrderDto>.SuccessResponse(await MapToDtoAsync(order, ct));
        }

        private async Task<OrderDto> MapToDtoAsync(Order order, CancellationToken ct)
        {
            var items = await _uow.Repository<OrderItem>().ListNoTrackingAsync(x => x.OrderId == order.Id, ct);
            return MapToDto(order, items.OrderBy(x => x.Id).ToList());
        }

        private static OrderDto MapToDto(Order order, IReadOnlyList<OrderItem> items)
            => new(
                order.Id,
                order.OrderNumber,
                order.Status,
                order.PaymentStatus,
                order.PaymentMethod,
                order.Source,
                order.Intent,
                order.RequiresManualConfirmation,
                order.IsViewedByAdmin,
                order.AdminViewedAt,
                order.AcceptedTerms,
                order.TermsAcceptedAt,
                order.FullName,
                order.Phone,
                order.Email,
                order.DeliveryMethod,
                order.CityOrRegion,
                order.District,
                order.StreetAndBuilding,
                order.ApartmentOrOffice,
                order.DeliveryNotes,
                order.PickupLocation,
                order.ProductsSubtotal,
                order.DeliveryFee,
                order.DiscountTotal,
                order.FinalTotal,
                order.CreatedAt,
                order.UpdatedAt,
                items.Select(x => new OrderItemDto(
                    x.Id,
                    x.ProductId,
                    x.ProductParametrId,
                    x.ProductName,
                    x.ProductImageUrl,
                    x.SelectedPower,
                    x.Quantity,
                    x.ReservedQuantity,
                    x.InventoryReleased,
                    x.UnitPrice,
                    x.LineTotal)).ToList());

        private static string ValidateCreateRequest(OrderCreateRequest request)
        {
            if (request?.Contact is null || request.Delivery is null || request.Items is null || request.Items.Count == 0)
            {
                return "Contact, delivery, payment, and at least one item are required.";
            }

            if (string.IsNullOrWhiteSpace(request.Contact.FullName))
            {
                return "Full name is required.";
            }

            if (!new EmailAddressAttribute().IsValid(request.Contact.Email))
            {
                return "A valid email is required.";
            }

            if (!AzerbaijanPhoneRegex.IsMatch(NormalizePhone(request.Contact.Phone)))
            {
                return "A valid Azerbaijan phone number is required.";
            }

            if (!Enum.IsDefined(typeof(OrderDeliveryMethod), request.Delivery.Method))
            {
                return "Delivery method is required.";
            }

            if (!Enum.IsDefined(typeof(OrderPaymentMethod), request.PaymentMethod))
            {
                return "Payment method is required.";
            }

            if (request.PaymentMethod != (byte)OrderPaymentMethod.PaymentAfterConfirmation)
            {
                return "Only manager contact payment is currently available.";
            }

            if (!Enum.IsDefined(typeof(OrderSource), request.Source))
            {
                return "Order source is required.";
            }

            if (!Enum.IsDefined(typeof(OrderIntent), request.Intent))
            {
                return "Order intent is required.";
            }

            if (request.Intent == (byte)OrderIntent.Purchase && !request.AcceptedTerms)
            {
                return "Purchase terms must be accepted.";
            }

            if (request.Delivery.Method == (byte)OrderDeliveryMethod.DeliveryToAddress)
            {
                if (string.IsNullOrWhiteSpace(request.Delivery.CityOrRegion) ||
                    string.IsNullOrWhiteSpace(request.Delivery.District) ||
                    string.IsNullOrWhiteSpace(request.Delivery.StreetAndBuilding))
                {
                    return "City, district, and street/building are required for delivery.";
                }
            }

            if (request.Delivery.Method == (byte)OrderDeliveryMethod.Pickup &&
                string.IsNullOrWhiteSpace(request.Delivery.PickupLocation))
            {
                return "Pickup location is required.";
            }

            if (request.Items.Any(x => x.ProductId <= 0 || x.Quantity <= 0))
            {
                return "Every order item must have a valid product and quantity.";
            }

            return null;
        }

        private static byte ResolveRequestedIntent(OrderCreateRequest request)
            => Enum.IsDefined(typeof(OrderIntent), request.Intent)
                ? request.Intent
                : (byte)OrderIntent.Purchase;

        private async Task ReleaseReservedInventoryAsync(IReadOnlyList<OrderItem> items, CancellationToken ct)
        {
            foreach (var item in items.Where(x => x.ReservedQuantity > 0 && !x.InventoryReleased && x.ProductParametrId is not null))
            {
                var parameter = await _uow.Repository<ProductParametr>()
                    .FirstOrDefaultAsync(x => x.Id == item.ProductParametrId.Value && x.IsActive, ct);
                if (parameter is null)
                {
                    continue;
                }

                parameter.Count = parameter.Count.GetValueOrDefault() + item.ReservedQuantity;
                _uow.Repository<ProductParametr>().Update(parameter);

                item.InventoryReleased = true;
                _uow.Repository<OrderItem>().Update(item);
                await SyncProductStockFlagAsync(parameter.ProductId, parameter.Id, parameter.Count.GetValueOrDefault(), ct);
            }
        }

        private async Task<string> TryReserveInventoryAsync(IReadOnlyList<OrderItem> items, CancellationToken ct)
        {
            foreach (var item in items.Where(x => x.ReservedQuantity > 0 && x.InventoryReleased && x.ProductParametrId is not null))
            {
                var parameter = await _uow.Repository<ProductParametr>()
                    .FirstOrDefaultAsync(x => x.Id == item.ProductParametrId.Value && x.IsActive, ct);
                if (parameter is null || parameter.Count.GetValueOrDefault() < item.ReservedQuantity)
                {
                    return $"Insufficient stock for {item.ProductName}.";
                }

                parameter.Count = parameter.Count.GetValueOrDefault() - item.ReservedQuantity;
                _uow.Repository<ProductParametr>().Update(parameter);

                item.InventoryReleased = false;
                _uow.Repository<OrderItem>().Update(item);
                await SyncProductStockFlagAsync(parameter.ProductId, parameter.Id, parameter.Count.GetValueOrDefault(), ct);
            }

            return null;
        }

        private async Task SyncProductStockFlagAsync(int productId, int changedParameterId, int changedCount, CancellationToken ct)
        {
            var product = await _uow.Repository<Product>().FirstOrDefaultAsync(x => x.Id == productId && x.IsActive, ct);
            if (product is null)
            {
                return;
            }

            var parameters = await _uow.Repository<ProductParametr>()
                .ListNoTrackingAsync(x => x.ProductId == productId && x.IsActive, ct);
            var previousInStock = product.InStock;
            var nextInStock = parameters.Any(x => x.Id == changedParameterId
                ? changedCount > 0
                : x.Count.GetValueOrDefault() > 0);
            product.InStock = nextInStock;
            if (!nextInStock && previousInStock)
            {
                product.OutOfStockAt = DateTime.UtcNow;
            }
            if (nextInStock)
            {
                product.OutOfStockAt = null;
            }
            _uow.Repository<Product>().Update(product);
        }

        private static decimal? CalculateDeliveryFee(byte deliveryMethod, ref bool requiresManualConfirmation)
        {
            if (deliveryMethod == (byte)OrderDeliveryMethod.Pickup)
            {
                return 0m;
            }

            requiresManualConfirmation = true;
            return null;
        }

        private static byte ResolveInitialPaymentStatus(byte paymentMethod)
            => paymentMethod switch
            {
                (byte)OrderPaymentMethod.BankCard => (byte)OrderPaymentStatus.AwaitingProvider,
                (byte)OrderPaymentMethod.PaymentAfterConfirmation => (byte)OrderPaymentStatus.NotRequiredYet,
                (byte)OrderPaymentMethod.SalesConsultation => (byte)OrderPaymentStatus.NotRequiredYet,
                _ => (byte)OrderPaymentStatus.Pending
            };

        private static string NormalizePhone(string phone)
            => string.IsNullOrWhiteSpace(phone)
                ? string.Empty
                : phone.Trim()
                    .Replace(" ", string.Empty)
                    .Replace("-", string.Empty)
                    .Replace("(", string.Empty)
                    .Replace(")", string.Empty);

        private static string NormalizeNullable(string value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
