### Texty odpovědí Asterionu - cs (čeština)

## Follows
follow-event-project-update = Aktualizace projektů
follow-event-new-project = Nové projekty
follow-event-download-milestone = Milníky stažení projektů
follow-event-project-update-description = Nové verze projektů tvůrce
follow-event-new-project-description = Projekty, které tvůrce zveřejní
follow-event-download-milestone-description = Když některý z projektů tvůrce dosáhne milníku
follow-event-none = Žádné
follow-added =
    Sleduji **{ $name }** :white_check_mark: Sleduji { $count ->
        [one] { $count } projekt
        [few] { $count } projekty
       *[other] { $count } projektů
    }, zprávy budou posílány do { $channel }.
follow-added-sync-failed = Sleduji **{ $name }**, ale projekty se zatím nepodařilo načíst ({ $error }). Brzy to zkusím znovu.
follow-already-following = Tento server už sleduje **{ $name }**
follow-limit-reached = Tento server už sleduje maximální počet tvůrců: { $max }
follow-not-found = Tento server tohoto tvůrce nesleduje
follow-removed = Přestal jsem sledovat **{ $name }** :white_check_mark:
follow-project-not-found = **{ $name }** nemá projekt s ID { $project }
follow-list-empty = Tento server nesleduje žádné tvůrce. Pomocí **Sledovat tvůrce** můžeš začít sledovat uživatele Modrinthu.
follow-list-title = Sledovaní tvůrci
follow-list-description = Zprávy se posílají o všech projektech těchto tvůrců
follow-list-entry =
    Typ: { $type }
    Události: { $events }
    Kanál: { $channel }
    Projekty: { $projects } ({ $ignored } ignorováno)
follow-target-user = Uživatel Modrinthu
follow-target-organization = Organizace na Modrinthu
follow-button-follow-user = Sledovat uživatele

## Lists
list-page = Strana { $page } z { $count }
list-tab-projects = Projekty ({ $count })
list-tab-creators = Tvůrci ({ $count })
list-subscriptions-title = Odebírané projekty
list-subscriptions-description = Zprávy se posílají o těchto projektech
list-subscriptions-empty = Neodebíráš žádné projekty. Pomocí **Přidat projekt** nějaký začni odebírat.
list-subscription-entry = **{ $title }** ({ $id })
    V { $channel } od { $since }
entry-not-found = Tento server tento projekt neodebírá
entry-unsub-confirm-question = Přestat odebírat **{ $title }** ({ $id })?
entry-unsubscribed = Odebírání zpráv o **{ $title }** ({ $id }) bylo zrušeno :white_check_mark:
follow-remove-confirm-question = Přestat sledovat **{ $name }**?

## Settings panel
settings-follow-title = Nastavení sledování: { $name } ({ $type })
settings-subscription-title = Nastavení odběru: { $title } ({ $id })
settings-channel = Kanál
settings-channel-placeholder = Vyber kanál
settings-new-project-channel = Jiný kanál pro nové projekty
settings-new-project-channel-placeholder = Stejný jako kanál výše
settings-ping-role = Jiná role k označení
settings-ping-role-placeholder = Výchozí role k označení
settings-new-project-ping-role = Jiná role k označení u nových projektů
settings-new-project-ping-role-placeholder = Stejná jako role výše
settings-milestone-channel = Jiný kanál pro milníky
settings-milestone-channel-placeholder = Kanál milníků serveru
settings-events = Události
settings-events-placeholder = Žádné události
settings-release-filter = Typy vydání
settings-release-placeholder = Vyber typy vydání
settings-loader-filter = Loadery
settings-loader-placeholder = Všechny loadery
settings-none = Žádné

## Manage panel
manage-tab-general = Obecné
manage-tab-notifications = Oznámení
manage-add-project-title = Přidat projekt
manage-add-project-input = Projekt
manage-add-project-placeholder = Slug, ID nebo odkaz, např. sodium
manage-add-follow-title = Sledovat tvůrce
manage-add-follow-input = Uživatel Modrinthu
manage-add-follow-placeholder = Uživatelské jméno, ID nebo odkaz
manage-subscribed = Odebíráš **{ $title }** :white_check_mark: Zprávy budou posílány do { $channel }.
manage-already-subscribed = Tento server už odebírá **{ $title }**
follow-projects-title = Projekty uživatele { $name }
follow-projects-description = O ignorovaných projektech se zprávy neposílají
follow-projects-empty = Zatím nejsou známy žádné projekty, načtou se při příští synchronizaci
follow-project-entry = **{ $title }** ({ $id })
    :bell: Zprávy se posílají
follow-project-entry-ignored = **{ $title }** ({ $id })
    :no_bell: Ignorováno

## Server settings
server-general-title = Nastavení serveru
server-general-description = Kdo může spravovat Asterion, koho označuje a jakým jazykem mluví
server-manage-role = Role pro správu
server-manage-role-placeholder = Pouze administrátoři
server-ping-role = Výchozí role k označení
server-ping-role-placeholder = Nikdo není označen
server-language = Jazyk
server-language-default = Jazyk serveru nastavený v Discordu
server-danger-zone = Nebezpečná zóna
server-unsubscribe-all = Zrušit odběr všech projektů
server-unfollow-all = Přestat sledovat všechny tvůrce
server-unsubscribe-all-title = Zrušit odběr všech projektů
server-unsubscribe-all-warning =
    ## :warning: Tím se odstraní { $count ->
        [one] { $count } odběr
        [few] { $count } odběry
       *[other] { $count } odběrů
    }
    O žádném z odebíraných projektů již nebudou chodit zprávy o aktualizacích a nastavení všech odběrů (kanál, role k označení, filtry) se smaže. Tuto akci nelze vrátit zpět.
server-unsubscribe-all-word = odebrat
server-unfollow-all-title = Přestat sledovat všechny tvůrce
server-unfollow-all-warning =
    ## :warning: Tím se odstraní { $count ->
        [one] { $count } sledování
        [few] { $count } sledování
       *[other] { $count } sledování
    }
    O žádném ze sledovaných tvůrců již nebudou chodit zprávy a nastavení všech sledování (kanály, role k označení, ignorované projekty, filtry) se smaže. Tuto akci nelze vrátit zpět.
server-unfollow-all-word = nesledovat
server-danger-confirm-input = Pro potvrzení napiš „{ $word }“
server-unsubscribed-all = Zrušen odběr { $count ->
        [one] { $count } projektu
       *[other] { $count } projektů
    } :white_check_mark:
server-unfollowed-all = Přestal jsem sledovat { $count ->
        [one] { $count } tvůrce
        [few] { $count } tvůrce
       *[other] { $count } tvůrců
    } :white_check_mark:
server-notifications-title = Zprávy o aktualizacích
server-notifications-description = Jak vypadají zprávy o nových verzích
server-message-style = Styl zprávy
server-changelog-style = Styl seznamu změn
server-search-title = Vyhledávání
server-subscribe-button = Tlačítko odběru
server-subscribe-button-shown = Zobrazeno pod výsledky `/search`
server-subscribe-button-hidden = Skryto pod výsledky `/search`
server-changelog-length = Délka seznamu změn
server-changelog-length-value = { $length } znaků
server-changelog-length-value-default = { $length } znaků (výchozí)
server-changelog-length-title = Délka seznamu změn
server-changelog-length-input = Maximální počet znaků
server-changelog-length-hint = Od { $min } do { $max }. Nechej prázdné pro obnovení výchozí hodnoty ({ $default }).
message-style-full = Úplný
message-style-compact = Kompaktní
changelog-style-plaintext = Prostý text
changelog-style-codeblock = Blok kódu
changelog-style-nochangelog = Bez seznamu změn
server-milestones-title = Milníky stažení
server-milestones = Zprávy o milnících
server-milestones-enabled = Posílají se, když projekt dosáhne milníku stažení
server-milestones-disabled = Neposílají se
server-milestones-override-hint = Kanál níže platí pro všechny projekty. Každý odebíraný projekt a sledovaný tvůrce ho může přepsat ve svém nastavení.
server-milestone-ladder = Milníky
server-milestone-channel = Kanál milníků
server-milestone-channel-placeholder = Kanál každého odběru nebo sledování
server-milestone-ping-role = Role k označení u milníků
server-milestone-ping-role-placeholder = Nikdo není označen
milestone-ladder-standard = Standardní: 1k, 2,5k, 5k, 10k, 25k…
milestone-ladder-round = Kulaté: 1k, 10k, 100k, 1M…
milestone-ladder-major = Hlavní: 100k, 1M, 10M…

## Buttons
button-add-project = Přidat projekt
button-follow-creator = Sledovat tvůrce
button-preview = Náhled
button-change = Změnit
button-hide = Skrýt
button-show = Zobrazit
button-enable = Zapnout
button-disable = Vypnout
button-milestones-on = Milníky: zapnuty
button-milestones-off = Milníky: vypnuty
button-milestones-server-off = Milníky: vypnuty na tomto serveru
button-ignore = Ignorovat
button-unignore = Přestat ignorovat
button-ignored-projects = Projekty ({ $ignored } z { $count } ignorováno)
button-settings = Nastavení
button-refresh = Obnovit
button-unfollow = Přestat sledovat
button-back = Zpět
button-unsubscribe = Zrušit odběr
button-confirm = Potvrdit
button-cancel = Zrušit
removal-cancelled = Zrušeno, nic nebylo odstraněno

## Charts
chart-caption-downloads = Stažení projektu **{ $title }** { $range ->
    [day] po hodinách za posledních 24 hodin
    [week] po 6 hodinách za posledních 7 dní
    [quarter] po dnech za posledních 90 dní
    [year] po týdnech za poslední rok
   *[month] po dnech za posledních 30 dní
} (UTC)
chart-caption-followers = Noví sledující projektu **{ $title }** { $range ->
    [day] po hodinách za posledních 24 hodin
    [week] po 6 hodinách za posledních 7 dní
    [quarter] po dnech za posledních 90 dní
    [year] po týdnech za poslední rok
   *[month] po dnech za posledních 30 dní
} (UTC)
