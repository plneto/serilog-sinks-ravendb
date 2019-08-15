using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Raven.Client;
using Serilog.Events;
using Serilog.Parsing;
using LogEvent = Serilog.Sinks.RavenDB.Data.LogEvent;

namespace Serilog.Sinks.RavenDB.Tests
{
    public class RavenDBSinkTests
    {
        private static readonly TimeSpan TinyWait = TimeSpan.FromMilliseconds(50);

        [OneTimeSetUp]
        public void Setup()
        {
            Raven.Embedded.EmbeddedServer.Instance.StartServer();
        }

        [Test]
        public void WhenAnEventIsWrittenToTheSinkItIsRetrievableFromTheDocumentStore()
        {
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(GenerateRandomDatabaseName()))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var exception = new ArgumentException("Ml�dek");
                const LogEventLevel level = LogEventLevel.Information;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var events = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).ToList();
                    Assert.AreEqual(1, events.Count);
                    var single = events.Single();
                    Assert.AreEqual(messageTemplate, single.MessageTemplate);
                    Assert.AreEqual("\"New Macabre\"++", single.RenderedMessage);
                    Assert.AreEqual(timestamp, single.Timestamp);
                    Assert.AreEqual(level, single.Level);
                    Assert.AreEqual(1, single.Properties.Count);
                    Assert.AreEqual("New Macabre", single.Properties["Song"]);
                    Assert.AreEqual(exception.Message, single.Exception.Message);
                }
            }
        }

        [Test]
        public void WhenAnEventIsWrittenWithExpirationItHasProperMetadata()
        {
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(GenerateRandomDatabaseName()))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var expiration = TimeSpan.FromDays(1);
                var errorExpiration = TimeSpan.FromMinutes(15);
                var targetExpiration = DateTime.UtcNow.Add(expiration);
                var exception = new ArgumentException("Ml�dek");
                const LogEventLevel level = LogEventLevel.Information;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null, expiration: expiration, errorExpiration: errorExpiration))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).First();
                    var metaData = session.Advanced.GetMetadataFor(logEvent)[Constants.Documents.Metadata.Expires].ToString();
                    var actualExpiration = Convert.ToDateTime(metaData).ToUniversalTime();
                    Assert.GreaterOrEqual(actualExpiration, targetExpiration, "The document should expire on or after {0} but expires {1}", targetExpiration, actualExpiration);
                }
            }
        }

        [Test]
        public void WhenAnErrorEventIsWrittenWithExpirationItHasProperMetadata()
        {
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(GenerateRandomDatabaseName()))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var errorExpiration = TimeSpan.FromDays(1);
                var expiration = TimeSpan.FromMinutes(15);
                var targetExpiration = DateTime.UtcNow.Add(errorExpiration);
                var exception = new ArgumentException("Ml�dek");
                const LogEventLevel level = LogEventLevel.Error;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null, expiration: expiration, errorExpiration: errorExpiration))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).First();
                    var metaData = session.Advanced.GetMetadataFor(logEvent)[Constants.Documents.Metadata.Expires].ToString();
                    var actualExpiration = Convert.ToDateTime(metaData).ToUniversalTime();
                    Assert.GreaterOrEqual(actualExpiration, targetExpiration, "The document should expire on or after {0} but expires {1}", targetExpiration, actualExpiration);
                }
            }
        }

        [Test]
        public void WhenAFatalEventIsWrittenWithExpirationItHasProperMetadata()
        {
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(GenerateRandomDatabaseName()))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var errorExpiration = TimeSpan.FromDays(1);
                var expiration = TimeSpan.FromMinutes(15);
                var targetExpiration = DateTime.UtcNow.Add(errorExpiration);
                var exception = new ArgumentException("Ml�dek");
                const LogEventLevel level = LogEventLevel.Fatal;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null, expiration: expiration, errorExpiration: errorExpiration))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).First();
                    var metaData = session.Advanced.GetMetadataFor(logEvent)[Constants.Documents.Metadata.Expires].ToString();
                    var actualExpiration = Convert.ToDateTime(metaData).ToUniversalTime();
                    Assert.GreaterOrEqual(actualExpiration, targetExpiration, "The document should expire on or after {0} but expires {1}", targetExpiration, actualExpiration);
                }
            }
        }

        [Test]
        public void WhenNoErrorExpirationSetBuExpirationSetUseExpirationForErrors()
        {
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(GenerateRandomDatabaseName()))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var expiration = TimeSpan.FromMinutes(15);
                var targetExpiration = DateTime.UtcNow.Add(expiration);
                var exception = new ArgumentException("Ml�dek");
                const LogEventLevel level = LogEventLevel.Fatal;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null, expiration: expiration))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).First();
                    var metaData = session.Advanced.GetMetadataFor(logEvent)[Constants.Documents.Metadata.Expires].ToString();
                    var actualExpiration = Convert.ToDateTime(metaData).ToUniversalTime();
                    Assert.GreaterOrEqual(actualExpiration, targetExpiration, "The document should expire on or after {0} but expires {1}", targetExpiration, actualExpiration);
                }
            }
        }

        [Test]
        public void WhenNoExpirationSetBuErrorExpirationSetUseErrorExpirationForMessages()
        {
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(GenerateRandomDatabaseName()))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var errorExpiration = TimeSpan.FromMinutes(15);
                var targetExpiration = DateTime.UtcNow.Add(errorExpiration);
                var exception = new ArgumentException("Ml�dek");
                const LogEventLevel level = LogEventLevel.Information;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null, errorExpiration: errorExpiration))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).First();
                    var metaData = session.Advanced.GetMetadataFor(logEvent)[Constants.Documents.Metadata.Expires].ToString();
                    var actualExpiration = Convert.ToDateTime(metaData).ToUniversalTime();
                    Assert.GreaterOrEqual(actualExpiration, targetExpiration, "The document should expire on or after {0} but expires {1}", targetExpiration, actualExpiration);
                }
            }
        }

        [Test]
        public void WhenNoExpirationIsProvidedMessagesDontExpire()
        {
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(GenerateRandomDatabaseName()))
            {
                documentStore.Initialize();
                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var exception = new ArgumentException("Ml�dek");
                const LogEventLevel level = LogEventLevel.Error;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).First();
                    Assert.IsFalse(session.Advanced.GetMetadataFor(logEvent).ContainsKey(Constants.Documents.Metadata.Expires), "No expiration set");
                }
            }
        }

        [Test]
        public void WhenExpirationSetToInfiniteMessagesDontExpire()
        {
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(GenerateRandomDatabaseName()))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var expiration = Timeout.InfiniteTimeSpan;
                var targetExpiration = DateTime.UtcNow.Add(expiration);
                var exception = new ArgumentException("Ml�dek");
                const LogEventLevel level = LogEventLevel.Information;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null, expiration: expiration))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).First();
                    Assert.IsFalse(session.Advanced.GetMetadataFor(logEvent).ContainsKey(Constants.Documents.Metadata.Expires), "No expiration set");
                }
            }
        }

        [Test]
        public void WhenErrorExpirationSetToInfiniteErrorsDontExpire()
        {
            using (var documentStore = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(GenerateRandomDatabaseName()))
            {
                documentStore.Initialize();

                var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
                var errorExpiration = Timeout.InfiniteTimeSpan;
                var targetExpiration = DateTime.UtcNow.Add(errorExpiration);
                var exception = new ArgumentException("Ml�dek");
                const LogEventLevel level = LogEventLevel.Information;
                const string messageTemplate = "{Song}++";
                var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };

                using (var ravenSink = new RavenDBSink(documentStore, 2, TinyWait, null, errorExpiration: errorExpiration))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                using (var session = documentStore.OpenSession())
                {
                    var logEvent = session.Query<LogEvent>().Customize(x => x.WaitForNonStaleResults()).First();
                    Assert.IsFalse(session.Advanced.GetMetadataFor(logEvent).ContainsKey(Constants.Documents.Metadata.Expires), "No expiration set");
                }
            }
        }

        [Test]
        public void WhenUsingConnectionStringInCtorInternalDocumentStoreIsCreated()
        {
            var timestamp = new DateTimeOffset(2013, 05, 28, 22, 10, 20, 666, TimeSpan.FromHours(10));
            var exception = new ArgumentException("Ml�dek");
            const LogEventLevel level = LogEventLevel.Information;
            const string messageTemplate = "{Song}++";
            var properties = new List<LogEventProperty> { new LogEventProperty("Song", new ScalarValue("New Macabre")) };
            var events = new Dictionary<string, LogEvent>();

            using (var store = Raven.Embedded.EmbeddedServer.Instance.GetDocumentStore(GenerateRandomDatabaseName()))
            {
                store.OnBeforeStore += (sender, e) => events[e.DocumentId] = (LogEvent)e.Entity;
                store.Initialize();

                using (var ravenSink = new RavenDBSink(store, 2, TinyWait, null))
                {
                    var template = new MessageTemplateParser().Parse(messageTemplate);
                    var logEvent = new Events.LogEvent(timestamp, level, exception, template, properties);
                    ravenSink.Emit(logEvent);
                }

                Assert.AreEqual(1, events.Count);
                var single = events.First().Value;
                Assert.AreEqual(messageTemplate, single.MessageTemplate);
                Assert.AreEqual("\"New Macabre\"++", single.RenderedMessage);
                Assert.AreEqual(timestamp, single.Timestamp);
                Assert.AreEqual(level, single.Level);
                Assert.AreEqual(1, single.Properties.Count);
                Assert.AreEqual("New Macabre", single.Properties["Song"]);
                Assert.AreEqual(exception.Message, single.Exception.Message);
            }
        }

        private static string GenerateRandomDatabaseName()
        {
            return Guid.NewGuid().ToString();
        }
    }
}