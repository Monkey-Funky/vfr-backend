using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Enums.Analytics;

/// <summary>
/// Lifecycle status for asynchronous dashboard report generation.
/// Stored as varchar(20).
/// </summary>
public enum ReportStatus
{
    Pending = 0,
    Processing = 1,
    Ready = 2,
    Failed = 3
}