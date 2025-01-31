﻿using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Extensibility;
using NServiceBus.Persistence;
using NServiceBus.Persistence.Sql;
using NServiceBus.Sagas;
using NUnit.Framework;

[TestFixture]
public class When_all_messages_have_finders : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_use_existing_saga()
    {
        if (!MsSqlMicrosoftDataClientConnectionBuilder.IsSql2016OrHigher())
        {
            return;
        }
        var context = await Scenario.Define<Context>()
            .WithEndpoint<SagaEndpoint>(b => b
                .When(session =>
                {
                    var startSagaMessage = new StartSagaMessage
                    {
                        Property = "Test"
                    };
                    return session.SendLocal(startSagaMessage);
                }))
            .Done(c => c.HandledOtherMessage)
            .Run()
            .ConfigureAwait(false);

        Assert.True(context.StartSagaFinderUsed);
        Assert.True(context.SomeOtherFinderUsed);
    }

    public class Context : ScenarioContext
    {
        public bool StartSagaFinderUsed { get; set; }
        public bool HandledOtherMessage { get; set; }
        public bool SomeOtherFinderUsed { get; set; }
    }

    public class SagaEndpoint : EndpointConfigurationBuilder
    {
        public SagaEndpoint()
        {
            EndpointSetup<DefaultServer>();
        }

        public class FindByStartSagaMessage : ISagaFinder<TestSaga.SagaData, StartSagaMessage>
        {
            Context testContext;

            public FindByStartSagaMessage(Context context)
            {
                testContext = context;
            }


            public Task<TestSaga.SagaData> FindBy(StartSagaMessage message, ISynchronizedStorageSession session, IReadOnlyContextBag context, CancellationToken cancellationToken = default)
            {
                testContext.StartSagaFinderUsed = true;

                return session.GetSagaData<TestSaga.SagaData>(
                    context: context,
                    whereClause: "json_value(Data,'$.Property') = @propertyValue",
                    appendParameters: (builder, append) =>
                    {
                        var parameter = builder();
                        parameter.ParameterName = "propertyValue";
                        parameter.Value = "Test";
                        append(parameter);
                    }, cancellationToken);
            }
        }

        public class FindBySomeOtherMessage : ISagaFinder<TestSaga.SagaData, SomeOtherMessage>
        {
            Context testContext;

            public FindBySomeOtherMessage(Context context)
            {
                testContext = context;
            }

            public Task<TestSaga.SagaData> FindBy(SomeOtherMessage message, ISynchronizedStorageSession session, IReadOnlyContextBag context, CancellationToken cancellationToken = default)
            {
                testContext.SomeOtherFinderUsed = true;

                return session.GetSagaData<TestSaga.SagaData>(
                    context: context,
                    whereClause: "json_value(Data,'$.Property') = @propertyValue",
                    appendParameters: (builder, append) =>
                    {
                        var parameter = builder();
                        parameter.ParameterName = "propertyValue";
                        parameter.Value = "Test";
                        append(parameter);
                    }, cancellationToken);
            }
        }

        public class TestSaga : SqlSaga<TestSaga.SagaData>,
            IAmStartedByMessages<StartSagaMessage>,
            IHandleMessages<SomeOtherMessage>
        {
            Context testContext;

            public TestSaga(Context context)
            {
                testContext = context;
            }


            public Task Handle(StartSagaMessage message, IMessageHandlerContext context)
            {
                Data.Property = message.Property;
                return context.SendLocal(new SomeOtherMessage
                {
                    Property = Data.Property
                });
            }

            public Task Handle(SomeOtherMessage message, IMessageHandlerContext context)
            {
                testContext.HandledOtherMessage = true;
                return Task.FromResult(0);
            }

            protected override string CorrelationPropertyName => null;

            protected override void ConfigureMapping(IMessagePropertyMapper mapper)
            {
            }

            public class SagaData : ContainSagaData
            {
                public string Property { get; set; }
            }
        }
    }

    public class StartSagaMessage : IMessage
    {
        public string Property { get; set; }
    }

    public class SomeOtherMessage : IMessage
    {
        public string Property { get; set; }
    }
}