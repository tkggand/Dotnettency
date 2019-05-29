﻿using Autofac;
using Autofac.Extensions.DependencyInjection;
using Dotnettency.Container;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Dotnettency
{
    public static class AutofacContainerBuilderOptionsExtensions
    {
        public static AdaptedContainerBuilderOptions<TTenant> Autofac<TTenant>(
            this ContainerBuilderOptions<TTenant> options,
            Action<TTenant, IServiceCollection> configureTenant)
            where TTenant : class
        {
            Func<ITenantContainerAdaptor> adaptorFactory = new Func<ITenantContainerAdaptor>(() =>
            {
                // host level container.
                ContainerBuilder builder = new ContainerBuilder();
                builder.Populate(options.Builder.Services);
                builder.AddDotnettencyContainerServices();


                // Build the root container.
                IContainer container = builder.Build();
                ITenantContainerAdaptor adaptedContainer = container.Resolve<ITenantContainerAdaptor>();

                // Get the service that allows us to publish events relating to tenant container events.
                container.TryResolve<ITenantContainerEventsPublisher<TTenant>>(out ITenantContainerEventsPublisher<TTenant> containerEventsPublisher);

                // Update the root container with a service that can be used to build per tenant container!
                ContainerBuilder updateBuilder = new ContainerBuilder();
                var defaultServices = options.DefaultServices;
                updateBuilder.RegisterInstance(new TenantContainerBuilder<TTenant>(defaultServices, adaptedContainer, configureTenant, containerEventsPublisher)).As<ITenantContainerBuilder<TTenant>>();
                updateBuilder.Update(container);


                // ITenantContainerAdaptor adaptor = container.Resolve<ITenantContainerAdaptor>();
                // return adaptor;

                return adaptedContainer;
            });

            AdaptedContainerBuilderOptions<TTenant> adapted = new AdaptedContainerBuilderOptions<TTenant>(options, adaptorFactory);
            return adapted;
        }
    }
}
