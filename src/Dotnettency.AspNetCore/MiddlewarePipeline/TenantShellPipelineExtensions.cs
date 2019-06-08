﻿using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace Dotnettency.AspNetCore.MiddlewarePipeline
{
    public static class TenantShellPipelineExtensions
    {
        public static Lazy<Task<RequestDelegate>> GetOrAddMiddlewarePipeline<TTenant>(this TenantShell<TTenant> tenantShell, Lazy<Task<RequestDelegate>> requestDelegateFactory)
            where TTenant : class
        {         
            return tenantShell.GetOrAddProperty<Lazy<Task<RequestDelegate>>>(nameof(TenantShellPipelineExtensions), requestDelegateFactory);           
        }
    }
}