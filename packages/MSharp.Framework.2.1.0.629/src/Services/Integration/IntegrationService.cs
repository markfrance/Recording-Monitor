namespace MSharp.Framework.Services
{
    public class IntegrationService<TRequest, TResponse>
    {
        /// <summary>
        /// Inserts a queu item to call this service and waits until the item is processed.
        /// Then it will return the response.
        /// </summary>
        public static TResponse Request(TRequest request)
        {
            return IntegrationManager.Request<TRequest, TResponse>(request);
        }

        /// <summary>
        /// Registers an integration service implementor.
        /// </summary>
        public static void RegisterImplementor<TService>() where TService : IServiceImplementor<TRequest, TResponse>
        {
            IntegrationManager.Register<TRequest, TResponse, TService>();
        }

        /// <summary>
        /// Injects an asyncronous waiter which will inject the provided response for one potential future request.
        /// It will check every 5 milliseconds to see if a request item is inserted in the queue, and in that case respond to it.
        /// </summary>
        public static void InjectResponse(TResponse injectedResponse)
        {
            IntegrationManager.InjectResponse<TRequest, TResponse>(injectedResponse);
        }

        /// <summary>
        /// It will wait until a response is provided by another thread to the integration queue item specified by its token.
        /// </summary>
        public static TResponse AwaitResponse(string requestToken, int waitIntervals = 50)
        {
            return IntegrationManager.AwaitResponse<TResponse>(requestToken, waitIntervals);
        }

        /// <summary>
        /// Inserts a request in the queue and immediately returns without waiting for a response.
        /// It will return the token string for this request, that can be queried later on for a response (using Await Response).
        /// </summary>
        public static string RequestAsync(TRequest request)
        {
            return IntegrationManager.RequestAsync<TRequest, TResponse>(request);
        }

    }
}
