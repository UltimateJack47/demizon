import 'package:dio/dio.dart';

/// Text chyby pro uživatele: server posílá `{ error }` česky, jinak obecná hláška.
String apiErrorText(Object error, String fallback) {
  if (error is DioException) {
    final data = error.response?.data;
    if (data is Map && data['error'] is String) return data['error'] as String;
    if (error.type == DioExceptionType.connectionError ||
        error.type == DioExceptionType.connectionTimeout ||
        error.type == DioExceptionType.receiveTimeout) {
      return 'Nepodařilo se spojit se serverem.';
    }
  }
  return fallback;
}
