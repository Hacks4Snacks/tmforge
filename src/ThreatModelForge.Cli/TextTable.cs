namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Renders simple left-aligned, space-padded text tables for human-readable CLI output.
    /// </summary>
    internal static class TextTable
    {
        /// <summary>
        /// Renders a header row followed by the supplied rows, padding each column to the widest
        /// cell it contains.
        /// </summary>
        /// <param name="headers">The column headers.</param>
        /// <param name="rows">The data rows; each row should have the same number of cells as
        /// <paramref name="headers"/>.</param>
        /// <returns>The formatted, multi-line table.</returns>
        public static string Render(string[] headers, IReadOnlyList<string[]> rows)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            int[] widths = new int[headers.Length];
            for (int i = 0; i < headers.Length; i++)
            {
                widths[i] = headers[i].Length;
            }

            foreach (string[] row in rows)
            {
                for (int i = 0; i < headers.Length && i < row.Length; i++)
                {
                    widths[i] = Math.Max(widths[i], (row[i] ?? string.Empty).Length);
                }
            }

            StringBuilder builder = new StringBuilder();
            AppendRow(builder, headers, widths);
            foreach (string[] row in rows)
            {
                AppendRow(builder, row, widths);
            }

            return builder.ToString().TrimEnd('\r', '\n');
        }

        private static void AppendRow(StringBuilder builder, string[] cells, int[] widths)
        {
            for (int i = 0; i < widths.Length; i++)
            {
                string cell = (i < cells.Length ? cells[i] : null) ?? string.Empty;
                builder.Append(cell);
                if (i < widths.Length - 1)
                {
                    builder.Append(new string(' ', (widths[i] - cell.Length) + 2));
                }
            }

            builder.Append('\n');
        }
    }
}
