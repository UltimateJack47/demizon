/// Jediný zdroj pravdy pro cesty v aplikaci.
///
/// Protějšek `Demizon.Maui/AppRoutes.cs` — ale bez jeho omezení. Komentář
/// v MAUI verzi varoval, že route NESMÍ obsahovat "/", protože by Shell první
/// segment interpretoval jako tab. go_router tímto netrpí, takže cesty jsou
/// normální hierarchické URL.
abstract final class AppRoutes {
  static const splash = '/splash';
  static const login = '/login';

  static const attendance = '/attendance';
  static const attendanceOverview = '/attendance/overview';
  static const attendanceStats = '/attendance/stats';
  static const memberAttendanceDetail = '/attendance/member';

  static const events = '/events';
  static const eventDetail = '/events/:id';
  static const eventCreate = '/events/create';
  static const eventEdit = '/events/:id/edit';

  static const dances = '/dances';
  static const danceDetail = '/dances/:id';

  static const gallery = '/gallery';
  static const photoViewer = '/gallery/viewer';

  static const profile = '/profile';
  static const profileEdit = '/profile/edit';
  static const changePassword = '/profile/password';

  static String eventDetailFor(int id) => '/events/$id';
  static String eventEditFor(int id) => '/events/$id/edit';
  static String danceDetailFor(int id) => '/dances/$id';
}
