using System;
using System.Collections.Generic;

namespace DurablePizzaFunctions.Functions.Pizzas
{
    public class Order
    {
        public Guid Id { get; set; }

        public Guid CustomerId { get; set; }

        public OrderStatus OrderStatus { get; set; }

        public List<OrderItem> OrderItems { get; set; }

        public DateTime OrderCreated { get; set; }
    }
}
