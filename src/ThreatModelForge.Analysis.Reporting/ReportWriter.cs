namespace ThreatModelForge.Analysis.Reporting
{
    using System;

    /// <summary>
    /// Abstract base class for classes that write reports.
    /// </summary>
    public abstract class ReportWriter : IDisposable
    {
        /// <summary>
        /// Finalizes an instance of the <see cref="ReportWriter"/> class.
        /// </summary>
        ~ReportWriter()
        {
            this.Dispose(false);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Writes the report.
        /// </summary>
        /// <param name="report">The report to write.</param>
        public abstract void Write(ModelReport report);

        /// <summary>
        /// Disposes resources owned by this instance.
        /// </summary>
        /// <param name="disposing">
        /// <c>True</c> to dispose both managed and unmanaged resource;
        /// <c>false</c> to dispose only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
