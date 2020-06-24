﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using HotChocolate.Execution;
using HotChocolate.Execution.Configuration;
using HotChocolate.StarWars;
using Xunit;

namespace HotChocolate.Tests
{
    public static class TestHelper
    {
        public static Task<IExecutionResult> ExpectValid(
            string query,
            Action<IRequestExecutorBuilder>? configure = null,
            Action<IQueryRequestBuilder>? request = null,
            IServiceProvider? requestServices = null)
        {
            return ExpectValid(
                query,
                new TestConfiguration
                {
                    ConfigureRequest = request,
                    Configure = configure,
                    Services = requestServices,
                });
        }

        public static async Task<IExecutionResult> ExpectValid(
            string query,
            TestConfiguration? configuration)
        {
            // arrange
            IRequestExecutor executor = await CreateExecutorAsync(configuration);
            IReadOnlyQueryRequest request = CreateRequest(configuration, query);

            // act
            IExecutionResult result = await executor.ExecuteAsync(request, default);

            // assert
            Assert.Null(result.Errors);
            return result;
        }

        public static Task ExpectError(
            string sdl,
            string query,
            Action<IRequestExecutorBuilder>? configure = null,
            Action<IQueryRequestBuilder>? request = null,
            IServiceProvider? requestServices = null,
            params Action<IError>[] elementInspectors) =>
            ExpectError(
                query,
                b =>
                {
                    b.AddDocumentFromString(sdl).UseNothing();
                    configure?.Invoke(b);
                },
                request,
                requestServices,
                elementInspectors);

        public static Task ExpectError(
            string query,
            Action<IRequestExecutorBuilder>? configure = null,
            Action<IQueryRequestBuilder>? request = null,
            IServiceProvider? requestServices = null,
            params Action<IError>[] elementInspectors)
        {
            return ExpectError(
                query,
                new TestConfiguration
                {
                    Configure = configure,
                    ConfigureRequest = request,
                    Services = requestServices
                },
                elementInspectors);
        }

        public static async Task ExpectError(
            string query,
            TestConfiguration? configuration,
            params Action<IError>[] elementInspectors)
        {
            // arrange
            IRequestExecutor executor = await CreateExecutorAsync(configuration);
            IReadOnlyQueryRequest request = CreateRequest(configuration, query);

            // act
            IExecutionResult result = await executor.ExecuteAsync(request, default);

            // assert
            Assert.NotNull(result.Errors);

            if (elementInspectors.Length > 0)
            {
                Assert.Collection(result.Errors, elementInspectors);
            }

            result.MatchSnapshot();
        }

        public static async Task<IRequestExecutor> CreateExecutorAsync(
            Action<IRequestExecutorBuilder>? configure = null)
        {
            var configuration  =new TestConfiguration
            {
                Configure = configure,
            };

            return await CreateExecutorAsync(configuration);
        }

        private static async ValueTask<IRequestExecutor> CreateExecutorAsync(
            TestConfiguration? configuration)
        {
            IRequestExecutorBuilder builder = new ServiceCollection().AddGraphQL();

            if (configuration?.Configure is { } c)
            {
                c.Invoke(builder);
            }
            else
            {
                AddDefaultConfiguration(builder);
            }

            return await builder.Services
                .BuildServiceProvider()
                .GetRequiredService<IRequestExecutorResolver>()
                .GetRequestExecutorAsync();
        }

        private static IReadOnlyQueryRequest CreateRequest(
            TestConfiguration? configuration, string query)
        {
            configuration ??= new TestConfiguration();

            IQueryRequestBuilder builder = QueryRequestBuilder.New().SetQuery(query);

            if (configuration.Services is { } services)
            {
                builder.SetServices(services);
            }

            if (configuration.ConfigureRequest is { } configure)
            {
                configure(builder);
            }

            return builder.Create();
        }

        public static void AddDefaultConfiguration(IRequestExecutorBuilder builder)
        {
            builder
                .AddStarWarsTypes()
                .AddInMemorySubscriptions()
                .Services
                .AddStarWarsRepositories();
        }
    }
}