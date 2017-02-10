using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OrderEntryMockingPractice.Services
{
    public partial class OrderService
    {
        public class OrderPlacementValidationException : Exception
        {
            public OrderPlacementValidationException(List<string> reasons) : 
                base(string.Join("\n", reasons.ToArray()))
            {
                this.Reasons = reasons;
            }

            public List<string> Reasons { get; private set; }
        }
    }
}