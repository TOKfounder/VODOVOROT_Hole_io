Локальный просмотр Водоворота

Из корня проекта:
python3 Tools/Preview/server.py --build builds/ImprovementPass-20261107/Yandex --port 8770

Открыть http://localhost:8770/?qaAds=1
qaAds=1 включает явно помеченные тестовые рекламные диалоги (success / duplicate / cancel / error).
Добавить &qaMobile=1 для мобильной ветки UI. Это эмуляция SDK, не проверка физического телефона.
Сервер принимает только loopback Host и GET/HEAD, не выдаёт файлы вне build/QA-root.
Адаптер не входит в WebGL build и никогда не должен включаться в production ZIP.
Реальные auth/cloud/IAP/рейтинг отключены. Сохранения гостя изолированы origin/портом.
Не считать номинальные цены в authored UI подтверждением каталога платформы.
Сама сборка не хранится в Git; её нужно получить через Unity Build Profiles/CLI.

Параметры и интеграционная проверка
Tools → Vodovorot → Round experience → Open isolated tuning lab
Дождаться сцены Assets/Tests/RoundExperienceLab.unity. Она использует настоящий City,
отдельный профиль в памяти и Music/SFX=0. Restart/Reset/Save относятся к рабочим
параметрам стенда; Save меняет RoundExperienceSettings.asset намеренно.
CLI runner: Tools/Unity/CheckRoundExperience.cs, entry CheckRoundExperience.Run.
Он восстанавливает исходные параметры, проверяет неизменность личного save и останавливает Play.
Перед runner явно проверить версию/готовность CLI и адресовать --project-path абсолютным корнем.
Компонентные fixtures не заменяют фактическое прохождение WebGL.
