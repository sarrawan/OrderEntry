using System;
using System.Collections.Generic;
using System.Linq;
using OrderEntryMockingPractice.Models;

namespace OrderEntryMockingPractice.Services
{
    public partial class OrderService
    {
        private readonly IOrderFulfillmentService _orderFulfillmentService;
        private readonly IProductRepository _productRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly ITaxRateService _taxRateService;
        private readonly IEmailService _emailService;

        public List<string> ExceptionReasons;

        public OrderService(IOrderFulfillmentService orderFulfillment, IProductRepository productRepository, ICustomerRepository customerRepository, ITaxRateService taxRateService, IEmailService emailService)
        {
            _orderFulfillmentService = orderFulfillment;
            _productRepository = productRepository;
            _customerRepository = customerRepository;
            _taxRateService = taxRateService;
            _emailService = emailService;
            ExceptionReasons = new List<string>();
        }

        public OrderSummary PlaceOrder(Order order)
        {
            decimal orderNetTotal = 0.0M;

            foreach (var orderItem in order.OrderItems)
            {
                bool check = _productRepository.IsInStock("laptop");
                if (!_productRepository.IsInStock("laptop"))
                {
                    ExceptionReasons.Add("There is not enough stock available for the product " + orderItem.Product.Sku + " to complete the order");
                }
                orderNetTotal += orderItem.Product.Price * orderItem.Quantity;
            }
            
            var customer = _customerRepository.Get((int) order.CustomerId);
            if (customer == null)
            {
                ExceptionReasons.Add("Customer not found");
                throw new OrderPlacementValidationException(ExceptionReasons);
            }

            var taxEntryList = _taxRateService.GetTaxEntries(customer.PostalCode, customer.Country);
            if (taxEntryList == null)
            {
                ExceptionReasons.Add("Tax Entry for the specified Postal Code: " + customer.PostalCode + " and Country: " + customer.Country + " was invalid");
                throw new OrderPlacementValidationException(ExceptionReasons);
            }

            var total = taxEntryList.Sum(t => t.Rate * orderNetTotal);

            var orderConf = _orderFulfillmentService.Fulfill(order);

            var orderSummary = new OrderSummary
            {
                CustomerId = orderConf.CustomerId,
                EstimatedDeliveryDate = DateTime.Now,
                NetTotal = orderNetTotal, 
                OrderId = orderConf.OrderId,
                OrderItems = order.OrderItems,
                OrderNumber = orderConf.OrderNumber,
                Total = total,
                Taxes = taxEntryList
            };
            
            _emailService.SendOrderConfirmationEmail(orderSummary.CustomerId, orderSummary.OrderId);

            return orderSummary;
        }

    }
}
