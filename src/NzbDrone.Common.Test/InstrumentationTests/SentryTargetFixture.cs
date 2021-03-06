using NUnit.Framework;
using FluentAssertions;
using NzbDrone.Common.Instrumentation.Sentry;
using System;
using NLog;
using NzbDrone.Test.Common;
using System.Globalization;
using System.Linq;

namespace NzbDrone.Common.Test.InstrumentationTests
{
    [TestFixture]
    public class SentryTargetFixture : TestBase
    {
        private SentryTarget Subject;

        private static LogLevel[] AllLevels = LogLevel.AllLevels.ToArray();
        private static LogLevel[] SentryLevels = LogLevel.AllLevels.Where(x => x >= LogLevel.Error).ToArray();
        private static LogLevel[] OtherLevels = AllLevels.Except(SentryLevels).ToArray();

        private static Exception[] FilteredExceptions = new Exception[] {
            new UnauthorizedAccessException(),
            new TinyIoC.TinyIoCResolutionException(typeof(string))
        };

        [SetUp]
        public void Setup()
        {
            Subject = new SentryTarget("https://aaaaaaaaaaaaaaaaaaaaaaaaaa@sentry.io/111111");
        }

        private LogEventInfo GivenLogEvent(LogLevel level, Exception ex, string message)
        {
            return LogEventInfo.Create(level, "SentryTest", ex, CultureInfo.InvariantCulture, message);
        }

        [Test, TestCaseSource("AllLevels")]
        public void log_without_error_is_not_sentry_event(LogLevel level)
        {
            Subject.IsSentryMessage(GivenLogEvent(level, null, "test")).Should().BeFalse();
        }

        [Test, TestCaseSource("SentryLevels")]
        public void error_or_worse_with_exception_is_sentry_event(LogLevel level)
        {
            Subject.IsSentryMessage(GivenLogEvent(level, new Exception(), "test")).Should().BeTrue();
        }

        [Test, TestCaseSource("OtherLevels")]
        public void less_than_error_with_exception_is_not_sentry_event(LogLevel level)
        {
            Subject.IsSentryMessage(GivenLogEvent(level, new Exception(), "test")).Should().BeFalse();
        }

        [Test, TestCaseSource("FilteredExceptions")]
        public void should_filter_event_for_filtered_exception_types(Exception ex)
        {
            var log = GivenLogEvent(LogLevel.Error, ex, "test");
            Subject.IsSentryMessage(log).Should().BeFalse();
        }

        [Test, TestCaseSource("FilteredExceptions")]
        public void should_not_filter_event_for_filtered_exception_types_if_filtering_disabled(Exception ex)
        {
            Subject.FilterEvents = false;
            var log = GivenLogEvent(LogLevel.Error, ex, "test");
            Subject.IsSentryMessage(log).Should().BeTrue();
        }

        [Test, TestCaseSource(typeof(SentryTarget), "FilteredExceptionMessages")]
        public void should_filter_event_for_filtered_exception_messages(string message)
        {
            var log = GivenLogEvent(LogLevel.Error, new Exception("aaaaaaa" + message + "bbbbbbb"), "test");
            Subject.IsSentryMessage(log).Should().BeFalse();
        }

        [TestCase("A message that isn't filtered")]
        [TestCase("Error")]
        public void should_not_filter_event_for_exception_messages_that_are_not_filtered(string message)
        {
            var log = GivenLogEvent(LogLevel.Error, new Exception(message), "test");
            Subject.IsSentryMessage(log).Should().BeTrue();
        }
    }
}