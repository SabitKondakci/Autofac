﻿// This software is part of the Autofac IoC container
// Copyright © 2018 Autofac Contributors
// http://autofac.org
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.

using System.Collections.Generic;
using System.Linq;
using Autofac.Builder;
using Autofac.Core;
using Autofac.Util;

namespace Autofac.Features.Decorators
{
    internal static class InstanceDecorator
    {
        internal static object TryDecorateRegistration(
            IComponentRegistration registration,
            object instance,
            IComponentContext context,
            IEnumerable<Parameter> parameters)
        {
            foreach (var service in registration.Services)
            {
                if (!(service is IServiceWithType serviceWithType)
                    || service is DecoratorService
                    || serviceWithType.ServiceType.IsGenericEnumerableInterfaceType()) continue;

                var registry = context.ComponentRegistry;
                var decoratorService = new DecoratorService(serviceWithType.ServiceType);
                var decoratorRegistrations = registry.RegistrationsFor(decoratorService).ToList();

                if (decoratorRegistrations.Count <= 0) continue;

                var resolveParameters = parameters as Parameter[] ?? parameters.ToArray();

                var decorators = registry.RegistrationsFor(decoratorService)
                    .OrderBy(r => r.GetRegistrationOrder())
                    .Select(r => new
                    {
                        Registration = r,
                        Service = r.Services.OfType<DecoratorService>().FirstOrDefault()
                    })
                    .ToList();

                if (decorators.Count == 0) return instance;

                var serviceType = serviceWithType.ServiceType;

                var decoratorContext = DecoratorContext.Create(instance.GetType(), serviceType, instance);

                foreach (var decorator in decorators)
                {
                    if (!decorator.Service.Condition(decoratorContext)) continue;

                    var serviceParameter = new TypedParameter(serviceType, instance);
                    var contextParameter = new TypedParameter(typeof(IDecoratorContext), decoratorContext);
                    var invokeParameters = resolveParameters.Concat(new Parameter[] { serviceParameter, contextParameter });
                    instance = context.ResolveComponent(decorator.Registration, invokeParameters);

                    decoratorContext = decoratorContext.UpdateContext(instance);
                }

                return instance;
            }

            return instance;
        }
    }
}
