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

/*

using OrderEntryMockingPractice.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OrderEntryMockingPractice.Services
{
    public class OrderService
    {
        private IOrderFulfillmentService _orderFulfillmentService;
        private IProductRepository _productRepository;
        private ICustomerRepository _customerRepository;
        private IEmailService _emailService;
        private ITaxRateService _taxRateService;

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
            // make sure valid order before actually placing order

            //  make sure valid customer associated with order
            var customer = _customerRepository.Get((int)order.CustomerId);
            if (customer == null)
            {
                ExceptionReasons.Add("Customer not found");
                throw new OrderPlacementValidationException(ExceptionReasons);
            }

            // make sure order items unique
            var orderItemsUnique = EnsureOrderItemProductsUnique(order.OrderItems);
            if(!orderItemsUnique)
                throw new OrderPlacementValidationException(ExceptionReasons);

            decimal orderNetTotal = 0.0M;

            foreach (var orderItem in order.OrderItems)
            {
                //bool check = _productRepository.IsInStock(orderItem.Product.Sku);
                if (!_productRepository.IsInStock(orderItem.Product.Sku))
                {
                    ExceptionReasons.Add("There is not enough stock available for the product sku '" + orderItem.Product.Sku + "' to complete the order");
                }
                orderNetTotal += orderItem.Product.Price * orderItem.Quantity;
            }

            if(ExceptionReasons.Count > 0)
            {
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

        public bool EnsureOrderItemProductsUnique(List<OrderItem> orderItems)
        {
            var allProductSKU = orderItems.Select(p => p.Product.Sku);
            var unique = allProductSKU.Count() == allProductSKU.Distinct().Count();

            if(!unique)
            {
                var duplicates = orderItems.GroupBy(x => x.Product.Sku).Where(y => y.Count() > 1).Select(z => z.Key);
                foreach(var item in duplicates)
                {
                    ExceptionReasons.Add("Product sku '" + item + "' is not unique in the order.");
                }
                return false;
            }
            return true;
        }
    }
}



*/
