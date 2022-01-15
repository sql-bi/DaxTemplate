using System;

namespace Dax.Template.Enums
{
    [Flags]
    public enum AutoScanEnum : short
    {
        /// <summary>
        /// Does not scan data to find min/max date
        /// </summary>
        Disabled = 0,
        /// <summary>
        /// Scan OnlyTablesColumns excluding ExceptTablesColumns
        /// </summary>
        SelectedTablesColumns = 1,
        /// <summary>
        /// Scan active relationships connected to the Date table
        /// </summary>
        ScanActiveRelationships = 2,
        /// <summary>
        /// Scan inactive relationships connected to the Date table
        /// </summary>
        ScanInactiveRelationships = 4,
        Full = 127
    }
}
