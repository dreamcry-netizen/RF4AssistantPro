# Сборка Windows EXE v2.3.34

1. Установите **.NET 10 SDK x64**: <https://dotnet.microsoft.com/download/dotnet/10.0>
2. Распакуйте исходник в обычную папку с правом записи.
3. Запустите `BUILD_WINDOWS_EXE.cmd` двойным щелчком.
4. Сценарий выполнит runtime restore для `win-x64`, запустит интеграционные тесты и сделает self-contained публикацию.
5. Результат появится в `dist\RF4AssistantPro_v2.3.34_win-x64`.
6. Готовый архив: `dist\RF4AssistantPro_v2.3.34_win-x64.zip`.

Для запуска программы откройте `RF4AssistantPro.exe`. Устанавливать .NET Runtime отдельно не нужно: публикация автономная.
