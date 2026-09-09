namespace Demizon.Tests.Unit.Infrastructure;

/// <summary>
/// Testy, které přepisují <c>SixLabors.ImageSharp.Configuration.Default.MemoryAllocator</c>.
/// Ten je **globální pro proces**, takže se takové testy nesmí potkat paralelně.
/// </summary>
/// <remarks>
/// Bez téhle kolekce hrozily dvě konkrétní interleavings mezi
/// <c>CoreRegistrationTests</c> a <c>FileUploadServiceImageTests</c>:
/// <list type="bullet">
/// <item><description>
/// <c>AddCoreServices()</c> nasadí 128MB alokátor uprostřed dekódování a test
/// „obrázek nad stropem“ (16 MB) přestane selhat, jak má.
/// </description></item>
/// <item><description>
/// Registrační test si jako <c>original</c> uloží ten 16MB strop a po skončení
/// druhého testu ho vrátí — takže do zbytku běhu propíše 16MB limit.
/// </description></item>
/// </list>
/// </remarks>
[CollectionDefinition("ImageSharpGlobals")]
public sealed class ImageSharpGlobalsCollection;
