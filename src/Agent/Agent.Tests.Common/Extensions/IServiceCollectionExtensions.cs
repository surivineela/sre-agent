// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#pragma warning disable IDE0130 // Extension methods should be in the same namespace as the containing type
namespace Microsoft.Extensions.DependencyInjection
#pragma warning restore IDE0130 // Extension methods should be in the same namespace as the containing type
{
    public static class IServiceCollectionExtensions
    {
        /// <summary>
        /// Replaces all service registrations of type <typeparamref name="TService"/> with a single registration of type <typeparamref name="TNewImplementation"/>.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TNewImplementation"></typeparam>
        /// <param name="serviceCollection"></param>
        /// <param name="lifetime"></param>
        /// <returns></returns>
        public static IServiceCollection ReplaceAll<TService, TNewImplementation>(this IServiceCollection serviceCollection, ServiceLifetime lifetime)
        {
            serviceCollection
                .RemoveAll<TService>()
                .Add(new ServiceDescriptor(typeof(TService), typeof(TNewImplementation), lifetime));

            return serviceCollection;
        }

        /// <summary>
        /// Replaces all service registrations of type <typeparamref name="TService"/> with a single registration using an instance provided by <paramref name="factory"/>.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <param name="serviceCollection"></param>
        /// <param name="lifetime"></param>
        /// <param name="factory"></param>
        /// <returns></returns>
        public static IServiceCollection ReplaceAll<TService>(this IServiceCollection serviceCollection, ServiceLifetime lifetime, Func<IServiceProvider, object> factory)
        {
            serviceCollection
                .RemoveAll<TService>()
                .Add(new ServiceDescriptor(typeof(TService), factory, lifetime));

            return serviceCollection;
        }

        /// <summary>
        /// Replaces a single service registration of type <typeparamref name="TService"/> and implementation type <typeparamref name="TOldImplementation"/> with a single registration of type <typeparamref name="TNewImplementation"/>.
        /// If there is more than one registration of the given types, an <see cref="InvalidOperationException"/> will be thrown. Use <see cref="ReplaceAll{TService, TNewImplementation}(IServiceCollection, ServiceLifetime)"/> to remove all registrations.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TOldImplementation"></typeparam>
        /// <typeparam name="TNewImplementation"></typeparam>
        /// <param name="serviceCollection"></param>
        /// <param name="lifetime"></param>
        /// <returns></returns>
        public static IServiceCollection Replace<TService, TOldImplementation, TNewImplementation>(this IServiceCollection serviceCollection, ServiceLifetime lifetime)
        {
            serviceCollection
                .Remove<TService, TOldImplementation>()
                .Add(new ServiceDescriptor(typeof(TService), typeof(TNewImplementation), lifetime));

            return serviceCollection;
        }

        /// <summary>
        /// Replaces a single service registration of type <typeparamref name="TService"/> and implementation type <paramref name="oldImplementationType"/> with a single registration of type <typeparamref name="TNewImplementation"/>.
        /// If there is more than one registration of the given types, an <see cref="InvalidOperationException"/> will be thrown. Use <see cref="ReplaceAll{TService, TNewImplementation}(IServiceCollection, ServiceLifetime)"/> to remove all registrations.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TNewImplementation"></typeparam>
        /// <param name="serviceCollection"></param>
        /// <param name="lifetime"></param>
        /// <param name="oldImplementationType"></param>
        /// <returns></returns>
        public static IServiceCollection Replace<TService, TNewImplementation>(this IServiceCollection serviceCollection, ServiceLifetime lifetime, Type oldImplementationType)
        {
            serviceCollection
                .Remove<TService>(oldImplementationType)
                .Add(new ServiceDescriptor(typeof(TService), typeof(TNewImplementation), lifetime));

            return serviceCollection;
        }

        /// <summary>
        /// Replaces a single service registration of type <typeparamref name="TService"/> and implementation type <typeparamref name="TOldImplementation"/> with a single registration provided by the given <paramref name="factory"/>.
        /// If there is more than one registration of the given types, an <see cref="InvalidOperationException"/> will be thrown. Use <see cref="ReplaceAll{TService, TNewImplementation}(IServiceCollection, ServiceLifetime)"/> to remove all registrations.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TOldImplementation"></typeparam>
        /// <param name="serviceCollection"></param>
        /// <param name="lifetime"></param>
        /// <param name="factory"></param>
        /// <returns></returns>
        public static IServiceCollection Replace<TService, TOldImplementation>(this IServiceCollection serviceCollection, ServiceLifetime lifetime, Func<IServiceProvider, object> factory)
        {
            serviceCollection
                .Remove<TService, TOldImplementation>()
                .Add(new ServiceDescriptor(typeof(TService), factory, lifetime));

            return serviceCollection;
        }

        /// <summary>
        /// Removes a single service registration of type <typeparamref name="TService"/> and implementation type <typeparamref name="TImplementation"/>.
        /// If there is more than one registration, of the given types, an <see cref="InvalidOperationException"/> will be thrown. Use <see cref="RemoveAll{TService, TImplementation}(IServiceCollection)"/> to remove all registrations.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TImplementation"></typeparam>
        /// <param name="serviceCollection"></param>
        /// <returns></returns>
        public static IServiceCollection Remove<TService, TImplementation>(this IServiceCollection serviceCollection)
        {
            ServiceDescriptor descriptor = serviceCollection.SingleOrDefault(x => x.ServiceType == typeof(TService) && x.ImplementationType == typeof(TImplementation));

            if (descriptor != null)
            {
                serviceCollection.Remove(descriptor);
            }

            return serviceCollection;
        }

        /// <summary>
        /// Removes a single service registration of type <typeparamref name="TService"/> and implementation type <paramref name="oldImplementationType"/>.
        /// If there is more than one registration, of the given types, an <see cref="InvalidOperationException"/> will be thrown. Use <see cref="RemoveAll{TService}(IServiceCollection, Type)"/> to remove all registrations.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <param name="serviceCollection"></param>
        /// <param name="oldImplementationType"></param>
        /// <returns></returns>
        public static IServiceCollection Remove<TService>(this IServiceCollection serviceCollection, Type oldImplementationType)
        {
            ServiceDescriptor descriptor = serviceCollection.SingleOrDefault(x => x.ServiceType == typeof(TService) && x.ImplementationType == oldImplementationType);

            if (descriptor != null)
            {
                serviceCollection.Remove(descriptor);
            }

            return serviceCollection;
        }

        /// <summary>
        /// Removes all services registered with type <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <param name="serviceCollection"></param>
        /// <returns></returns>
        public static IServiceCollection RemoveAll<TService>(this IServiceCollection serviceCollection)
        {
            foreach (ServiceDescriptor descriptor in serviceCollection.Where(x => x.ServiceType == typeof(TService)).ToList())
            {
                serviceCollection.Remove(descriptor);
            }

            return serviceCollection;
        }

        /// <summary>
        /// Removes all services registered with type <typeparamref name="TService"/> and implementation type <typeparamref name="TImplementation"/>.
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TImplementation"></typeparam>
        /// <param name="serviceCollection"></param>
        /// <returns></returns>
        public static IServiceCollection RemoveAll<TService, TImplementation>(this IServiceCollection serviceCollection)
        {
            foreach (ServiceDescriptor descriptor in serviceCollection.Where(x => x.ServiceType == typeof(TService) && x.ImplementationType == typeof(TImplementation)).ToList())
            {
                serviceCollection.Remove(descriptor);
            }

            return serviceCollection;
        }

        /// <summary>
        /// Removes all services registered with type <typeparamref name="TService"/> and implementation type <paramref name="oldImplementationType"/>.
        /// /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <param name="serviceCollection"></param>
        /// <param name="oldImplementationType"></param>
        /// <returns></returns>
        public static IServiceCollection RemoveAll<TService>(this IServiceCollection serviceCollection, Type oldImplementationType)
        {
            var descriptors = serviceCollection.Where(x => x.ServiceType == typeof(TService) && x.ImplementationType == oldImplementationType).ToList();
            foreach (ServiceDescriptor descriptor in descriptors)
            {
                serviceCollection.Remove(descriptor);
            }

            return serviceCollection;
        }
    }
}
