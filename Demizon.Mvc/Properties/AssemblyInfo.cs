using System.Runtime.CompilerServices;

// Kontrakt docházky ("yes"/"maybe"/"no") se parsuje v AttendancesController.ParseStatus.
// Je to doménové pravidlo, jehož tichá regrese by znamenala špatně zapsanou účast,
// takže je testovatelné z unit projektu bez toho, aby muselo být public.
[assembly: InternalsVisibleTo("Demizon.Tests.Unit")]
