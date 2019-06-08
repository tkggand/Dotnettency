﻿using Dotnettency.AspNetCore.MiddlewarePipeline;
using Dotnettency.MiddlewarePipeline;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Dotnettency
{
    public static class TenantPipelineOptionsBuilderExtensions
    {
        public static MultitenancyOptionsBuilder<TTenant> AspNetCorePipeline<TTenant>(this TenantPipelineOptionsBuilder<TTenant> builder, Action<TenantPipelineBuilderContext<TTenant>, IApplicationBuilder> configuration)
            where TTenant : class
        {
            var factory = new DelegateTenantMiddlewarePipelineFactory<TTenant>(configuration);
            // builder.
            builder.MultitenancyOptions.Services.AddSingleton<ITenantMiddlewarePipelineFactory<TTenant, IApplicationBuilder, RequestDelegate>>(factory);
            builder.MultitenancyOptions.Services.AddScoped<ITenantPipelineAccessor<TTenant, IApplicationBuilder, RequestDelegate>, TenantPipelineAccessor<TTenant>>();
            return builder.MultitenancyOptions;
        }
    }
}