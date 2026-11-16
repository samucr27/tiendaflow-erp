using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TiendaFlow.Domain;
using TiendaFlow.Web.Formatting;

namespace TiendaFlow.IntegrationTests;

/// <summary>
/// Guards the contract of the centralized translator: known domain errors keep
/// their actionable Spanish message and are not logged, while an unexpected
/// exception is logged with its type, message and stack trace before the
/// generic message is returned to the user.
/// </summary>
public class DomainErrorTranslatorTests
{
    [Fact]
    public void UnknownExceptionIsLoggedWithItsTypeMessageAndStack()
    {
        var logger = new RecordingLogger();
        DomainErrorTranslator.UseLogger(logger);

        try
        {
            Exception exception;

            try
            {
                throw new InvalidOperationException("detalle técnico interno");
            }
            catch (InvalidOperationException caught)
            {
                exception = caught;
            }

            var message = DomainErrorTranslator.ToSpanish(exception);

            Assert.Equal("Ocurrió un error inesperado. Intenta de nuevo.", message);

            var entry = Assert.Single(logger.Entries);
            Assert.Same(exception, entry.Exception);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Contains("InvalidOperationException", entry.Message);
            Assert.Contains(
                nameof(DomainErrorTranslatorTests),
                exception.StackTrace ?? string.Empty,
                StringComparison.Ordinal);
        }
        finally
        {
            DomainErrorTranslator.UseLogger(NullLogger.Instance);
        }
    }

    [Fact]
    public void KnownDomainExceptionKeepsItsMessageAndIsNotLogged()
    {
        var logger = new RecordingLogger();
        DomainErrorTranslator.UseLogger(logger);

        try
        {
            var message = DomainErrorTranslator.ToSpanish(
                new InsufficientStockException("SKU-1001", available: 1, requested: 5));

            Assert.Equal("No hay unidades suficientes para completar la operación.", message);
            Assert.Empty(logger.Entries);
        }
        finally
        {
            DomainErrorTranslator.UseLogger(NullLogger.Instance);
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
