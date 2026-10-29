import 'package:flutter/material.dart';

/// Barevná paleta převzatá 1:1 z `Demizon.Maui/Resources/Styles/Colors.xaml`.
abstract final class DemizonColors {
  static const primary = Color(0xFFA8845E);
  static const primaryDark = Color(0xFF7A5A38);
  static const primaryLight = Color(0xFFD4B898);
  static const secondary = Color(0xFFB89470);
  static const secondaryDark = Color(0xFF8A6A48);
  static const accent = Color(0xFF9A7450);

  static const pageBackground = Color(0xFFFEFBF5);
  static const pageBackgroundDark = Color(0xFF1A1008);
  static const cardBackground = Color(0xFFFFFFFF);
  static const cardBackgroundDark = Color(0xFF2C1E12);
  static const warmBeige = Color(0xFFF5EEE0);
  static const warmBeigeDark = Color(0xFF3A2A18);

  static const textPrimary = Color(0xFF4A3420);
  static const textSecondary = Color(0xFF8A6848);
  static const textOnPrimary = Color(0xFFFFFFFF);

  /// Stavy docházky — v MAUI byly tyto hexy natvrdo rozeseté
  /// po `AllMembersAttendancePage.xaml.cs`. Tady patří do tématu.
  static const attendanceYes = Color(0xFF27AE60);
  static const attendanceNo = Color(0xFFE74C3C);
  static const attendanceMaybe = Color(0xFFF39C12);
  static const attendanceNone = Color(0xFFBDC3C7);

  /// Zlatý akcent, kterým se v přehledech odlišují akce od zkoušek.
  /// V MAUI to byl hex `#C9A227` opsaný ve dvou souborech.
  static const eventGold = Color(0xFFC9A227);

  static const error = Color(0xFFE74C3C);
  static const success = Color(0xFF27AE60);
  static const warning = Color(0xFFF39C12);
  static const info = Color(0xFF3498DB);
}

/// Barvy textu a podkladů, které se liší mezi světlým a tmavým režimem.
/// `DemizonColors.textPrimary` / `textSecondary` jsou jen pro světlý — natvrdo
/// použité dávaly v tmavém režimu tmavě hnědý text na tmavě hnědé kartě.
@immutable
class DemizonPalette extends ThemeExtension<DemizonPalette> {
  const DemizonPalette({
    required this.textPrimary,
    required this.textSecondary,
    required this.warmBeige,
    required this.accentText,
  });

  final Color textPrimary;
  final Color textSecondary;

  /// Teplý podklad zvýrazněných bloků (text písně, popis, prázdný stav).
  final Color warmBeige;

  /// Zvýrazněný text v barvě značky (region tance apod.).
  final Color accentText;

  static const light = DemizonPalette(
    textPrimary: DemizonColors.textPrimary,
    textSecondary: DemizonColors.textSecondary,
    warmBeige: DemizonColors.warmBeige,
    accentText: DemizonColors.primaryDark,
  );

  static const dark = DemizonPalette(
    textPrimary: Color(0xFFFBF6EE),
    textSecondary: Color(0xFFCDB89C),
    warmBeige: DemizonColors.warmBeigeDark,
    accentText: DemizonColors.primaryLight,
  );

  @override
  DemizonPalette copyWith({Color? textPrimary, Color? textSecondary, Color? warmBeige, Color? accentText}) =>
      DemizonPalette(
        textPrimary: textPrimary ?? this.textPrimary,
        textSecondary: textSecondary ?? this.textSecondary,
        warmBeige: warmBeige ?? this.warmBeige,
        accentText: accentText ?? this.accentText,
      );

  @override
  DemizonPalette lerp(DemizonPalette? other, double t) => other == null
      ? this
      : DemizonPalette(
          textPrimary: Color.lerp(textPrimary, other.textPrimary, t)!,
          textSecondary: Color.lerp(textSecondary, other.textSecondary, t)!,
          warmBeige: Color.lerp(warmBeige, other.warmBeige, t)!,
          accentText: Color.lerp(accentText, other.accentText, t)!,
        );
}

extension DemizonPaletteContext on BuildContext {
  DemizonPalette get palette => Theme.of(this).extension<DemizonPalette>() ?? DemizonPalette.light;
}

abstract final class DemizonTheme {
  static ThemeData get light => _build(Brightness.light);
  static ThemeData get dark => _build(Brightness.dark);

  static ThemeData _build(Brightness brightness) {
    final isDark = brightness == Brightness.dark;
    final scheme = ColorScheme.fromSeed(
      seedColor: DemizonColors.primary,
      brightness: brightness,
    ).copyWith(
      primary: isDark ? DemizonColors.primaryLight : DemizonColors.primary,
      secondary: DemizonColors.secondary,
      error: DemizonColors.error,
      surface: isDark ? DemizonColors.cardBackgroundDark : DemizonColors.cardBackground,
    );

    return ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      extensions: [isDark ? DemizonPalette.dark : DemizonPalette.light],
      scaffoldBackgroundColor:
          isDark ? DemizonColors.pageBackgroundDark : DemizonColors.pageBackground,
      appBarTheme: AppBarTheme(
        backgroundColor: isDark ? DemizonColors.primaryDark : DemizonColors.primary,
        foregroundColor: DemizonColors.textOnPrimary,
        elevation: 0,
        centerTitle: false,
      ),
      cardTheme: CardThemeData(
        color: isDark ? DemizonColors.cardBackgroundDark : DemizonColors.cardBackground,
        elevation: 1,
        margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      ),
      inputDecorationTheme: InputDecorationTheme(
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(8)),
        filled: true,
        fillColor: isDark ? DemizonColors.warmBeigeDark : DemizonColors.warmBeige,
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size.fromHeight(48),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
        ),
      ),
    );
  }

  /// Barva pro stav docházky. Kontrakt statusů je shodný s API:
  /// "yes" | "maybe" | "no" | null.
  static Color attendanceColor(String? status) => switch (status) {
        'yes' => DemizonColors.attendanceYes,
        'maybe' => DemizonColors.attendanceMaybe,
        'no' => DemizonColors.attendanceNo,
        _ => DemizonColors.attendanceNone,
      };
}
