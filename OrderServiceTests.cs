

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using FizzWare.NBuilder;
using NUnit.Framework;
using NSubstitute;
using NUnit.Framework.Constraints;
using OrderEntryMockingPractice.Models;
using OrderEntryMockingPractice.Services;
using Shouldly;

namespace OrderEntryMockingPracticeTests
{
    [TestFixture]
    public class OrderServiceTests
    {
        private const string PostalCode = "12345";
        private const string Country = "USA";
        private const string OrderNumber = "orderNum1";
        private const int OrderId = 123;


        private ICustomerRepository _customerRepository;
        private IOrderFulfillmentService _orderFulfillmentService;
        private IProductRepository _productRepository;
        private ITaxRateService _taxRateService;
        private IEmailService _emailService;
        OrderService orderService;

        [SetUp]
        public void SetUp()
        {
            _customerRepository = Substitute.For<ICustomerRepository>();
            _productRepository = Substitute.For<IProductRepository>();
            _orderFulfillmentService = Substitute.For<IOrderFulfillmentService>();
            _taxRateService = Substitute.For<ITaxRateService>();
            _emailService = Substitute.For<IEmailService>();

            this.orderService = new OrderService(this._orderFulfillmentService, this._productRepository, this._customerRepository, this._taxRateService, this._emailService);
        }

        [Test]
        public void OrderItemsUnique_ByProductSKU()
        {
            // Arrange
            var order = ValidOrderSetup();

            // Act
            var results = orderService.PlaceOrder(order);

            // Assert
            bool uniqueProductSKU = EnsureOrderItemProductsUnique(results.OrderItems);
            uniqueProductSKU.ShouldBe(true);
        }

       //// 
        [Test]
        public void AllProductsInOrderAreInStock()
        {
            // Arrange
            var order = ValidOrderSetup();

            // Act
            var results = orderService.PlaceOrder(order);
            
            // Assert
            _productRepository.IsInStock(Arg.Any<string>()).ShouldBe(true);
        }

        [Test]
        public void WhenProductsAreNotUnique()
        {
            var order = CreateValidOrder();
            order.OrderItems.Add(order.OrderItems[0]); // add duplicate item

            var reasons = Assert.Throws<OrderService.OrderPlacementValidationException>(() =>
            {
                orderService.PlaceOrder(new Order());
            }).Reasons;

            Assert.That(reasons, Has.Member("Product sku 'fredbob' is not unique in the order."));
        }

        [Test]
        public void WhenAProductIsNotInStock()
        {
            Assert.Fail("not implemented");
        }

        [Test]
        public void WhenMoreThanOneProductIsNotInStock()
        {
            Assert.Fail("not implemented: I should get a separate message for each product.");
        }

        [Test]
        public void InvalidOrder_ExceptionListThrown()
        {
            // Arrange
            var order = InvalidOrderSetup();

            // Act and Assert
            Assert.Throws<OrderService.OrderPlacementValidationException>(() =>
            {
                orderService.PlaceOrder(order);
            });
        }

        [Test]
        public void ValidOrder_OrderSummaryReturned()
        {
            // Arrange
            var order = ValidOrderSetup();

            // Act
            var results = orderService.PlaceOrder(order);

            // Assert
            Assert.IsInstanceOf<OrderSummary>(results);
        }

        [Test]
        public void ValidOrder_OrderSummaryReturned_AndSubmittedToOrderFulfillmentService()
        {
            // Arrange
            var order = ValidOrderSetup();

            // Act
            var results = orderService.PlaceOrder(order);

            // Assert
            _orderFulfillmentService.Received().Fulfill(order);
        }

        [Test]
        public void ValidOrder_OrderSummaryReturned_AndContainsConfirmationNumber()
        {
            //Arrange
            var order = ValidOrderSetup();

            // Act
            var results = orderService.PlaceOrder(order);

            // Assert
            results.OrderNumber.ShouldBe(OrderNumber);
        }


        [Test]
        public void ValidOrder_OrderSummaryReturned_AndContainsIDGeneratedByOrderFulfillmentService()
        {
            // Arrange
            var order = ValidOrderSetup();

            // Act
            var results = orderService.PlaceOrder(order);
            
            // Assert
            results.OrderId.ShouldBe(OrderId);
        }

        [Test]
        public void ValidOrder_OrderSummaryReturned_AndContainsApplicableTaxesForCustomer()
        {
            // Arrange
            var order = ValidOrderSetup();
            
            var taxEntryList = CreateTaxEntryList();
            
            // Act
            var result = orderService.PlaceOrder(order);

            // Assert
            result.Taxes.Count().ShouldBe(taxEntryList.Count);
        }

        [Test]
        public void TaxEntryForCustomerIsValidAndNotEmpty()
        {
            // Arrange
            var order = ValidOrderSetup();

            // Act
            var result = orderService.PlaceOrder(order);

            // Assert
            result.Taxes.Count().ShouldBeGreaterThan(0);
            result.CustomerId.ShouldBe((int)order.CustomerId);        
        }

        [Test]
        public void ValidOrder_OrderSummaryReturned_AndHasCorrectNetTotal()
        {
            // Arrange
            var order = ValidOrderSetup();

            var expectedNetTotal = CalculateNetTotal(order);

            // Act
            var results = orderService.PlaceOrder(order);

            // Assert
            results.NetTotal.ShouldBe(expectedNetTotal);
        }

        [Test]
        public void ValidOrder_OrderSummaryReturned_AndHasCorrectOrderTotal()
        {
            // Arrange
            var order = ValidOrderSetup();

            var taxEntryList = CreateTaxEntryList();

            var netTotal = CalculateNetTotal(order);
            var expectedOrderTotal = taxEntryList.Sum(t => t.Rate * netTotal); 

            // Act
            var results = orderService.PlaceOrder(order);

            // Assert
            results.Total.ShouldBe(expectedOrderTotal);
        }

        [Test]
        public void ValidOrder_OrderSummaryReturned_AndConfirmationEmailSentToCustomer()
        {
            // Arrange 
            var order = ValidOrderSetup();

            // Act
            var results = orderService.PlaceOrder(order);

            // Assert
            _emailService.Received().SendOrderConfirmationEmail(results.CustomerId, results.OrderId);
        }

        [Test]
        public void CustomerInfoCanBeRetrievedFromCustomerRepository()
        {
            // Arrange
            var order = ValidOrderSetup();

            // Act
            var results = orderService.PlaceOrder(order);

            // Assert
            _customerRepository.Received().Get(Arg.Any<int>());
            
        }

        [Test]
        public void TaxesCanBeRetrievedFromTaxRateServiceRepository()
        {
            // Arrange
            var order = ValidOrderSetup();

            // Act
            var results = orderService.PlaceOrder(order);

            // Assert
            _taxRateService.Received().GetTaxEntries(Arg.Any<string>(), Arg.Any<string>());
        }

        [Test]
        public void ProductRepositoryCanBeUsedToDetermineProductsInStock()
        {
            // Arrange
            var order = ValidOrderSetup();

            // Act
            var results = orderService.PlaceOrder(order);
            
            // Assert
            _productRepository.Received().IsInStock(Arg.Any<string>());
        }

        private static decimal CalculateNetTotal(Order order)
        {
            decimal expectedNetTotal = 0.0M;
            foreach (var orderItem in order.OrderItems)
            {
                expectedNetTotal += orderItem.Product.Price * orderItem.Quantity;
            }
            return expectedNetTotal;
        }

        private static Customer CreateCustomer(string postalCode, string country)
        {
            //return new Customer()
            //{
            //    CustomerId = 100,
            //    PostalCode = "12345",
            //    Country = "USA"
            //};

            var customer = Builder<Customer>
                .CreateNew()
                .Do(c => c.PostalCode = postalCode)
                .Do(c => c.Country = country)
                .Build();

            return customer;
        }

        private static TaxEntry CreateTaxEntry()
        {
            return new TaxEntry
            {
                Description = "This is a test Tax Entry 1",
                Rate = 1.2M
            };
        }

        private static List<TaxEntry> CreateTaxEntryList()
        {
            return new List<TaxEntry> { CreateTaxEntry(), CreateTaxEntry(), CreateTaxEntry()};
        }
        
        private Order ValidOrderSetup()
        {
            _customerRepository.Get(Arg.Any<int>())
                .Returns(CreateCustomer(PostalCode, Country));

            _productRepository.IsInStock(Arg.Any<string>()).Returns(true);

            _orderFulfillmentService.Fulfill(Arg.Any<Order>())
                .Returns(new OrderConfirmation() { OrderNumber = "orderNum1", OrderId = 123, CustomerId = 30 });

            _taxRateService.GetTaxEntries(Arg.Any<string>(), Arg.Any<string>()).Returns(CreateTaxEntryList());
            return CreateValidOrder();
        }

        private Order InvalidOrderSetup()
        {
            _customerRepository.Get(Arg.Any<int>())
                .Returns(c => null);

            _productRepository.IsInStock(Arg.Any<string>()).Returns(false);

            _orderFulfillmentService.Fulfill(Arg.Any<Order>()).Returns(o => null);

            _taxRateService.GetTaxEntries(Arg.Any<string>(), Arg.Any<string>()).Returns(t => null);

            return null;
        }

        private static Order CreateValidOrder()
        {
            var order = new Order
            {
                CustomerId = 30,
                OrderItems = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Product = new Product
                        {
                            ProductId = 210,
                            Sku = "Laptop",
                            Price = 200M
                        },
                        Quantity = 100
                    },
                    new OrderItem
                    {
                        Product = new Product
                        {
                            ProductId = 370,
                            Sku = "Tablet",
                            Price = 100M
                        },
                        Quantity = 200
                    }
                }
            };
            return order;
        }

        public bool EnsureOrderItemProductsUnique(List<OrderItem> orderItems)
        {
            List<String> productSkUsInOrder = new List<String>();
            foreach (var item in orderItems)
            {
                productSkUsInOrder.Add(item.Product.Sku);
            }

            return productSkUsInOrder.Distinct().Count() == productSkUsInOrder.Count;
        }


    }
}
