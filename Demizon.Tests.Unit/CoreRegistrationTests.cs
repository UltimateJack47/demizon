using Demizon.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;

namespace Demizon.Tests.Unit;

/// <summary>
/// <c>AddCoreServices</c> kromě registrace služeb nastavuje **globální** strop
/// alokátoru ImageSharpu. Ten strop je pojistka pro 1GB stroj: bez něj by
/// obrázek, na který nestačí ani škálovaný dekód, vyčerpal paměť kontejneru
/// a proces by zabilo jádro, místo aby upload skončil chybou.
/// </summary>
/// <remarks>
/// Chování stropu hlídá
/// <c>FileUploadServiceImageTests.UploadImageToDbAsync_obrazek_nad_stropem_alokatoru_vrati_srozumitelny_neuspech</c>,
/// který si ho lokálně sníží. Tady jde o to, že ho registrační cesta vůbec
/// nastaví — dosud nebyla pokrytá vůbec, takže její odstranění by žádný test
/// nezachytil.
/// <para>
/// Konkrétní hodnotu (128 MB) ImageSharp z alokátoru nezveřejňuje, takže se
/// ověřuje záměna instance, ne číslo.
/// </para>
/// </remarks>
public class CoreRegistrationTests
{
    [Fact]
    public void AddCoreServices_nastavi_globalni_strop_alokatoru_ImageSharpu()
    {
        // Globální stav se vrací zpátky, jinak by tenhle test ovlivnil ostatní
        // v témže běhu.
        var original = Configuration.Default.MemoryAllocator;
        var sentinel = MemoryAllocator.Create(new MemoryAllocatorOptions
        {
            AllocationLimitMegabytes = 7,
        });
        Configuration.Default.MemoryAllocator = sentinel;

        try
        {
            new ServiceCollection().AddCoreServices();

            Assert.NotSame(sentinel, Configuration.Default.MemoryAllocator);
        }
        finally
        {
            Configuration.Default.MemoryAllocator = original;
        }
    }

    [Fact]
    public void AddCoreServices_zaregistruje_sluzby_pouzivane_hostem()
    {
        var services = new ServiceCollection().AddCoreServices();

        // Výběr, ne výčet: kdyby některá z nich z registrace vypadla, host
        // spadne až za běhu při prvním requestu na dané stránce.
        var registered = services.Select(d => d.ServiceType).ToHashSet();
        Assert.Contains(typeof(Core.Services.Member.IMemberService), registered);
        Assert.Contains(typeof(Core.Services.Attendance.IAttendanceService), registered);
        Assert.Contains(typeof(Core.Services.File.IFileService), registered);
        Assert.Contains(typeof(Core.Services.FileUpload.IFileUploadService), registered);
        Assert.Contains(typeof(Core.Services.Storage.IStorageQuotaService), registered);
        Assert.Contains(typeof(Core.Services.Authentication.TokenService), registered);
    }
}
