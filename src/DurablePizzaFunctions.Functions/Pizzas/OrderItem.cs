namespace DurablePizzaFunctions.Functions.Pizzas
{
    public class OrderItem
    {
        public int Amount { get; set; } = 1;

        public Pizza Pizza { get; set; }
    }
}
