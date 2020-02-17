namespace DurablePizzaFunctions.Functions.Pizzas
{
    public enum OrderStatus
    {
        Created,
        AwaitingPayment,
        Paid,
        PaymentCancelled
    }
}
