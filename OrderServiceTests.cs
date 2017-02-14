// want to make "negative tests" --> so test for when there are no products in stock --> want it to be specific
// need the NUnit, Nsubstitute, and Shouldly packages


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
            
            // now want to make sure than an exception is thrown in PlaceOrder
            // move the ensure method to PlaceOrder so that the check can happen
            // there and not in the test, make sure the test only checks what happens
            // doesn't actually implement the checking 

            var reasons = Assert.Throws<OrderService.OrderPlacementValidationException>(() =>
            {
                orderService.PlaceOrder(new Order());
            }).Reasons;

            // this should be the name that you use, not fredbob, but maybe laptop?
            Assert.That(reasons, Has.Member("Product sku 'fredbob' is not unique in the order."));
        }

        [Test]
        public void WhenAProductIsNotInStock()
        {
             // do something like
            //_productRepository.IsInStock("laptop").Returns(true);
            //_productRepository.IsInStock("tablet").Returns(false);
            // symbolizes that laptop is in fact in stock and laptop is not in stock
            Assert.Fail("not implemented");
        }

        [Test]
        public void WhenMoreThanOneProductIsNotInStock()
        {
            // do something like
            //_productRepository.IsInStock("laptop").Returns(false);
            //_productRepository.IsInStock("tablet").Returns(false);
            //_productRepository.IsInStock("keyboard").Returns(false);
            
            // symbolizes that laptop is in fact in stock and laptop is not in stock
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

        // this test is fine, maybe dont want to refactor too much 
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

        // this test should be fine 
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

        // this test should be checked
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

        // this test should be checked
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

        // this test should be checked
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

        // this test should be checked
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
        
        // this test should be fine 
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

        // this test should be fine 
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

        // this test should be checked
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

        // this test should be checked
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

        // this test should be checked
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

        // this test should be checked
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
        
        // probably dont want to use something like this
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

        // probably dont want to use something like this
        private Order InvalidOrderSetup()
        {
            _customerRepository.Get(Arg.Any<int>())
                .Returns(c => null);

            _productRepository.IsInStock(Arg.Any<string>()).Returns(false);

            _orderFulfillmentService.Fulfill(Arg.Any<Order>()).Returns(o => null);

            _taxRateService.GetTaxEntries(Arg.Any<string>(), Arg.Any<string>()).Returns(t => null);

            return null;
        }

        // not sure if want to use something like this
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

        // this method should be removed from here, it should be eneterd
        // into the OrderService.PlaceOrder method, not here
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

/*

using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using NUnit.Framework;
using NSubstitute;
using OrderEntryMockingPractice.Models;
using OrderEntryMockingPractice.Services;
using Shouldly;
using System.Linq;

namespace OrderEntryMockingPracticeTests
{
    [TestFixture]
    public class OrderServiceTests
    {

        IOrderFulfillmentService _orderFulfillmentService;
        IEmailService _emailService;
        ICustomerRepository _customerRepository;
        IProductRepository _productRepository;
        ITaxRateService _taxRateService;

        OrderService orderService;

        private string expectedOrderNumber = "123";
        private int expectedOrderID = 1;
        private int expectedCustomerID = 10;
        private decimal expectedNetTotal = 400M;


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
        public void OneOrderItemNotUniqueByProductSKU()
        {
            // Arrange
            _customerRepository.Get(Arg.Any<int>()).Returns(new Customer() { CustomerId = expectedCustomerID });
            _productRepository.IsInStock(Arg.Any<string>()).Returns(true);
            _orderFulfillmentService.Fulfill(Arg.Any<Order>()).Returns(new OrderConfirmation());
            _taxRateService.GetTaxEntries(Arg.Any<string>(), Arg.Any<string>()).Returns(new List<TaxEntry> { new TaxEntry() });

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
                        Quantity = 1
                    },
                    new OrderItem
                    {
                        Product = new Product
                        {
                            ProductId = 370,
                            Sku = "Tablet",
                            Price = 100M
                        },
                        Quantity = 2
                    }
                }
            };

            order.OrderItems.Add(order.OrderItems[0]);

            var orderService = new OrderService(_orderFulfillmentService, _productRepository, _customerRepository, _taxRateService, _emailService);


            // Act and Assert
            var reasons = Assert.Throws<OrderPlacementValidationException>(() =>
            {
                orderService.PlaceOrder(order);
            }).Reasons;

            Assert.That(reasons, Has.Member("Product sku 'Laptop' is not unique in the order."));
        }

        [Test]
        public void MultipleOrderItemsNotUniqueByProductSKU()
        {
            // Arrange
            _customerRepository.Get(Arg.Any<int>()).Returns(new Customer() { CustomerId = expectedCustomerID });
            _productRepository.IsInStock(Arg.Any<string>()).Returns(true);
            _orderFulfillmentService.Fulfill(Arg.Any<Order>()).Returns(new OrderConfirmation());
            _taxRateService.GetTaxEntries(Arg.Any<string>(), Arg.Any<string>()).Returns(new List<TaxEntry> { new TaxEntry() });

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
                        Quantity = 1
                    },
                    new OrderItem
                    {
                        Product = new Product
                        {
                            ProductId = 370,
                            Sku = "Tablet",
                            Price = 100M
                        },
                        Quantity = 2
                    }
                }
            };

            order.OrderItems.Add(order.OrderItems[0]);
            order.OrderItems.Add(order.OrderItems[0]);
            order.OrderItems.Add(order.OrderItems[1]);

            var orderService = new OrderService(_orderFulfillmentService, _productRepository, _customerRepository, _taxRateService, _emailService);


            // Act and Assert
            var reasons = Assert.Throws<OrderPlacementValidationException>(() =>
            {
                orderService.PlaceOrder(order);
            }).Reasons;

            Assert.That(reasons, Has.Member("Product sku 'Laptop' is not unique in the order."));
            Assert.That(reasons, Has.Member("Product sku 'Tablet' is not unique in the order."));
        }

        [Test]
        public void WhenOneProductNotInStock()
        {
            // Arrange
            _customerRepository.Get(Arg.Any<int>()).Returns(new Customer() { CustomerId = expectedCustomerID } );
            _orderFulfillmentService.Fulfill(Arg.Any<Order>()).Returns(new OrderConfirmation());
            _taxRateService.GetTaxEntries(Arg.Any<string>(), Arg.Any<string>()).Returns(new List<TaxEntry> { new TaxEntry() });

            var orderService = new OrderService(_orderFulfillmentService, _productRepository, _customerRepository, _taxRateService, _emailService);

            var order = new Order
            {
                CustomerId = 30,
                OrderItems = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Product = new Product
                        {
                            Sku = "Laptop",
                        },
                    },
                    new OrderItem
                    {
                        Product = new Product
                        {
                            Sku = "Tablet",
                        },
                    }
                }
            };

            _productRepository.IsInStock("Laptop").Returns(true);
            _productRepository.IsInStock("Tablet").Returns(false);

            // Act and Assert
            var reasons = Assert.Throws<OrderPlacementValidationException>(() =>
            {
                orderService.PlaceOrder(order);
            }).Reasons;

            Assert.That(reasons, Has.Member("There is not enough stock available for the product sku 'Tablet' to complete the order"));
        }

        [Test]
        public void WhenMultipleProductsNotInStock()
        {
            // Arrange
            _customerRepository.Get(Arg.Any<int>()).Returns(new Customer() { CustomerId = expectedCustomerID });
            _orderFulfillmentService.Fulfill(Arg.Any<Order>()).Returns(new OrderConfirmation());
            _taxRateService.GetTaxEntries(Arg.Any<string>(), Arg.Any<string>()).Returns(new List<TaxEntry> { new TaxEntry() });

            var orderService = new OrderService(_orderFulfillmentService, _productRepository, _customerRepository, _taxRateService, _emailService);

            var order = new Order
            {
                CustomerId = 30,
                OrderItems = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Product = new Product
                        {
                            Sku = "Laptop",
                        },
                    },
                    new OrderItem
                    {
                        Product = new Product
                        {
                            Sku = "Tablet",
                        },
                    }
                }
            };

            _productRepository.IsInStock("Laptop").Returns(false);
            _productRepository.IsInStock("Tablet").Returns(false);

            // Act and Assert
            var reasons = Assert.Throws<OrderPlacementValidationException>(() =>
            {
                orderService.PlaceOrder(order);
            }).Reasons;

            Assert.That(reasons, Has.Member("There is not enough stock available for the product sku 'Tablet' to complete the order"));
            Assert.That(reasons, Has.Member("There is not enough stock available for the product sku 'Laptop' to complete the order"));
        }

        [Test]
        public void OrderInvalid_ExceptionWithListOfReasons()
        {
            _customerRepository.Get(expectedCustomerID).Returns(new Customer());
            _productRepository.IsInStock(Arg.Any<string>()).Returns(false);

            var orderService = new OrderService(_orderFulfillmentService, _productRepository, _customerRepository, _taxRateService, _emailService);

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

            // Act and Assert
            var reasons = Assert.Throws<OrderPlacementValidationException>(() =>
            {
                orderService.PlaceOrder(order);
            }).Reasons;
            reasons.Count().ShouldBeGreaterThan(0);
        }

        [Test]
        public void OrderValid_OrderSummaryReturned()
        {
            // Arrange

            ////// MIGHT WANT TO CHANGE THIS 
            _customerRepository.Get(Arg.Any<int>()).Returns(new Customer());
            _productRepository.IsInStock(Arg.Any<string>()).Returns(true);
            _orderFulfillmentService.Fulfill(Arg.Any<Order>()).Returns(new OrderConfirmation());
            _taxRateService.GetTaxEntries(Arg.Any<string>(), Arg.Any<string>()).Returns(new List<TaxEntry> { new TaxEntry() });

            var orderService = new OrderService(_orderFulfillmentService, _productRepository, _customerRepository, _taxRateService, _emailService);

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

            // Act
            var results = orderService.PlaceOrder(order);

            // Assert
            Assert.IsInstanceOf<OrderSummary>(results);
        }

        [Test]
        public void ValidOrder_OrderSummaryReturned_SubmittedToOrderFulfillmentService()
        {
            // Arrange
            _customerRepository.Get(Arg.Any<int>()).Returns(new Customer());
            _productRepository.IsInStock(Arg.Any<string>()).Returns(true);
            _orderFulfillmentService.Fulfill(Arg.Any<Order>()).Returns(new OrderConfirmation());
            _taxRateService.GetTaxEntries(Arg.Any<string>(), Arg.Any<string>()).Returns(new List<TaxEntry> { new TaxEntry() });

            var orderService = new OrderService(_orderFulfillmentService, _productRepository, _customerRepository, _taxRateService, _emailService);

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

            // Act
            var results = orderService.PlaceOrder(order);

            // Assert
            _orderFulfillmentService.Received().Fulfill(order);
        }

        [Test]
        public void ValidOrder_OrderSummaryReturned_ContainsOrderFulfillmentConfirmationNumber()
        {
            // Arrange
            _customerRepository.Get(Arg.Any<int>()).Returns(new Customer());
            _productRepository.IsInStock(Arg.Any<string>()).Returns(true);
            _orderFulfillmentService.Fulfill(Arg.Any<Order>()).Returns(new OrderConfirmation() { OrderNumber = expectedOrderNumber });
            _taxRateService.GetTaxEntries(Arg.Any<string>(), Arg.Any<string>()).Returns(new List<TaxEntry> { new TaxEntry() });

            var orderService = new OrderService(_orderFulfillmentService, _productRepository, _customerRepository, _taxRateService, _emailService);

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

            // Act
            var results = orderService.PlaceOrder(order);

            // Assert
            results.OrderNumber.ShouldBe(expectedOrderNumber);
        }

        [Test]
        public void ValidOrder_OrderSummaryReturned_ContainsIDGeneratedByOrderFulfillmentService()
        {
            // Arrange
            _customerRepository.Get(Arg.Any<int>()).Returns(new Customer());
            _productRepository.IsInStock(Arg.Any<string>()).Returns(true);
            _orderFulfillmentService.Fulfill(Arg.Any<Order>()).Returns(new OrderConfirmation() { OrderId = expectedOrderID});
            _taxRateService.GetTaxEntries(Arg.Any<string>(), Arg.Any<string>()).Returns(new List<TaxEntry> { new TaxEntry() });

            var orderService = new OrderService(_orderFulfillmentService, _productRepository, _customerRepository, _taxRateService, _emailService);

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

            // Act
            var results = orderService.PlaceOrder(order);

            // Assert
            results.OrderId.ShouldBe(expectedOrderID);

        }

        [Test]
        public void ValidOrder_OrderSummaryReturned_ContainsApplicableTaxes()
        {
            // Arrange
            _customerRepository.Get(Arg.Any<int>()).Returns(new Customer() { CustomerId = expectedCustomerID});
            _productRepository.IsInStock(Arg.Any<string>()).Returns(true);
            _orderFulfillmentService.Fulfill(Arg.Any<Order>()).Returns(new OrderConfirmation());

            var taxEntry = new TaxEntry { Description = "Sales", Rate = 1.2M };

            _taxRateService.GetTaxEntries(Arg.Any<string>(), Arg.Any<string>()).Returns(new List<TaxEntry> { taxEntry });

            var orderService = new OrderService(_orderFulfillmentService, _productRepository, _customerRepository, _taxRateService, _emailService);

            var order = new Order
            {
                CustomerId = expectedCustomerID,
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

            // Act
            var results = orderService.PlaceOrder(order);

            // Assert
            results.Taxes.ShouldContain(taxEntry);
        }

        [Test]
        public void ValidOrder_OrderSummaryReturned_NetTotalCalculated()
        {
            // Arrange
            _customerRepository.Get(Arg.Any<int>()).Returns(new Customer());
            _productRepository.IsInStock(Arg.Any<string>()).Returns(true);
            _orderFulfillmentService.Fulfill(Arg.Any<Order>()).Returns(new OrderConfirmation());
            _taxRateService.GetTaxEntries(Arg.Any<string>(), Arg.Any<string>()).Returns(new List<TaxEntry> { new TaxEntry() });

            var orderService = new OrderService(_orderFulfillmentService, _productRepository, _customerRepository, _taxRateService, _emailService);

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
                        Quantity = 1
                    },
                    new OrderItem
                    {
                        Product = new Product
                        {
                            ProductId = 370,
                            Sku = "Tablet",
                            Price = 100M
                        },
                        Quantity = 2
                    }
                }
            };

            // Act
            var results = orderService.PlaceOrder(order);

            // Assert
            results.NetTotal.ShouldBe(expectedNetTotal);
        }

        [Test]
        public void ValidOrder_OrderSummaryReturned_OrderTotalCalculated()
        {
            // Arrange
            var customer = new Customer
            {
                CustomerId = expectedCustomerID,
                PostalCode = "12345",
                Country = "USA"
            };
            _customerRepository.Get(Arg.Any<int>()).Returns(customer);
            _productRepository.IsInStock(Arg.Any<string>()).Returns(true);
            _orderFulfillmentService.Fulfill(Arg.Any<Order>()).Returns(new OrderConfirmation());

            var taxEntry1 = new TaxEntry { Rate = 1.2M };
            var taxEntry2 = new TaxEntry { Rate = 1.5M };
            var taxEntryList = new List<TaxEntry> { taxEntry1, taxEntry2 };
            
            _taxRateService.GetTaxEntries(customer.PostalCode, customer.Country).Returns(taxEntryList);

            var orderService = new OrderService(_orderFulfillmentService, _productRepository, _customerRepository, _taxRateService, _emailService);

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
                        Quantity = 1
                    },
                    new OrderItem
                    {
                        Product = new Product
                        {
                            ProductId = 370,
                            Sku = "Tablet",
                            Price = 100M
                        },
                        Quantity = 2
                    }
                }
            };

            var expectedOrderTotal = taxEntryList.Sum(t => t.Rate * expectedNetTotal);

            // Act
            var results = orderService.PlaceOrder(order);

            // Assert
            results.Total.ShouldBe(expectedOrderTotal);
        }

        [Test]
        public void ValidOrder_OrderSummaryReturned_ConfirmationEmailSentToCustomer()
        {
            // Arrange
            _customerRepository.Get(Arg.Any<int>()).Returns(new Customer());
            _productRepository.IsInStock(Arg.Any<string>()).Returns(true);
            _orderFulfillmentService.Fulfill(Arg.Any<Order>()).Returns(new OrderConfirmation() { OrderId = expectedOrderID, CustomerId = expectedCustomerID});
            _taxRateService.GetTaxEntries(Arg.Any<string>(), Arg.Any<string>()).Returns(new List<TaxEntry> { new TaxEntry() });

            var orderService = new OrderService(_orderFulfillmentService, _productRepository, _customerRepository, _taxRateService, _emailService);

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
                        Quantity = 1
                    },
                    new OrderItem
                    {
                        Product = new Product
                        {
                            ProductId = 370,
                            Sku = "Tablet",
                            Price = 100M
                        },
                        Quantity = 2
                    }
                }
            };

            // Act
            var results = orderService.PlaceOrder(order);

            // Assert
            _emailService.Received().SendOrderConfirmationEmail(expectedCustomerID, expectedOrderID);
        }

        [Test]
        public void CustomerInformationCannotBeRetrieved()
        {
            // Arrange
            _customerRepository.Get(expectedCustomerID).Returns(new Customer());
            _productRepository.IsInStock(Arg.Any<string>()).Returns(true);
            _orderFulfillmentService.Fulfill(Arg.Any<Order>()).Returns(new OrderConfirmation() { OrderId = expectedOrderID, CustomerId = expectedCustomerID });
            _taxRateService.GetTaxEntries(Arg.Any<string>(), Arg.Any<string>()).Returns(new List<TaxEntry> { new TaxEntry() });

            var orderService = new OrderService(_orderFulfillmentService, _productRepository, _customerRepository, _taxRateService, _emailService);

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
                        Quantity = 1
                    },
                    new OrderItem
                    {
                        Product = new Product
                        {
                            ProductId = 370,
                            Sku = "Tablet",
                            Price = 100M
                        },
                        Quantity = 2
                    }
                }
            };

            // Act and Assert
            var reasons = Assert.Throws<OrderPlacementValidationException>(() =>
            {
                orderService.PlaceOrder(order);
            }).Reasons;

            Assert.That(reasons, Has.Member("Customer not found"));
        }

        [Test]
        public void TaxEntryCannotBeRetrievedFromTaxRateService()
        {
            // Arrange
            var customer = new Customer() { PostalCode = "12345", Country = "USA" };
            _customerRepository.Get(Arg.Any<int>()).Returns(customer);
            _productRepository.IsInStock(Arg.Any<string>()).Returns(true);
            _orderFulfillmentService.Fulfill(Arg.Any<Order>()).Returns(new OrderConfirmation() { OrderId = expectedOrderID, CustomerId = expectedCustomerID });
            _taxRateService.GetTaxEntries(customer.PostalCode, customer.Country).Returns(new List<TaxEntry>() { new TaxEntry() });

            var orderService = new OrderService(_orderFulfillmentService, _productRepository, _customerRepository, _taxRateService, _emailService);

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
                        Quantity = 1
                    },
                    new OrderItem
                    {
                        Product = new Product
                        {
                            ProductId = 370,
                            Sku = "Tablet",
                            Price = 100M
                        },
                        Quantity = 2
                    }
                }
            };

            // Act 
            var reasons = orderService.PlaceOrder(order);

            // ASsert
            reasons.Taxes.Count().ShouldBe(1);            
        }

    }
}




*/
