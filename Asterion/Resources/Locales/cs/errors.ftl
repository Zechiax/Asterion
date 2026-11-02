### Texty chyb a varování Asterionu - cs (čeština)

## Generic
error-internal = Interní chyba, zkus to prosím později
error-unknown = Neznámá chyba, zkus to prosím později
error-command-failed = Při provádění tohoto příkazu došlo k chybě. Zkus to prosím později. Pokud problém přetrvává, kontaktuj vlastníka bota.
error-invalid-input = Možnosti příkazu nejsou platné, zkontroluj je a zkus to znovu
error-menu-outdated = Tato nabídka je zastaralá, nastavení změníš pomocí `/manage`

## Modrinth
error-modrinth-api-unavailable = Modrinth API je momentálně nedostupné, zkus to prosím později
error-search-unsuccessful = Vyhledávání se nezdařilo
error-search-no-result = Žádné výsledky pro dotaz „{ $query }“
error-project-not-found = Pro „{ $query }“ nebyl nalezen žádný projekt, zkontroluj slug nebo ID
error-no-more-results = Nenašly se žádné další projekty
error-message-empty = Zpráva neobsahuje žádný text, který by šlo vyhledat
error-input-is-project = To je odkaz na projekt, ne na uživatele. Odebírej raději přímo projekt, nebo použij odkaz na profil uživatele.

## Charts
error-no-stats-data = Pro tento projekt zatím nejsou uloženy žádné statistiky. Zaznamenávají se pouze tehdy, když ho nějaký server odebírá nebo sleduje jeho tvůrce
error-not-enough-data = Pro tento graf zatím není dost dat, zkus to znovu přibližně za { $unit ->
    [hours] { $amount ->
        [one] { $amount } hodinu
        [few] { $amount } hodiny
       *[other] { $amount } hodin
    }
   *[days] { $amount ->
        [one] { $amount } den
        [few] { $amount } dny
       *[other] { $amount } dní
    }
}

## Permissions
error-not-allowed = Tady to nemůžeš použít
error-guild-only = Toto lze použít pouze na serveru
error-manage-permission = K tomu potřebuješ buď oprávnění Administrátor, nebo roli pro správu Asterionu na tomto serveru
error-not-panel-owner = Toto může použít pouze uživatel, který příkaz spustil
error-invalid-channel = Při zpracování požadavku došlo k chybě, zkontroluj, zda je kanál správný
error-channel-not-text = Kanál musí být textový
error-bot-cannot-access-channel = Nevidím kanál { $channel } nebo v něm nemohu posílat zprávy, dej mi prosím potřebná oprávnění

## Settings
error-changelog-length-invalid = Zadej prosím číslo od { $min } do { $max }, nebo pole nech prázdné pro obnovení výchozí hodnoty
error-danger-not-confirmed = Potvrzovací slovo „{ $word }“ nebylo zadáno, nic nebylo odstraněno

## Warnings
warning-bot-cannot-send = Nemám oprávnění posílat zprávy v { $channel }, dej mi prosím potřebná oprávnění
warning-already-subscribed = Tento server už tento projekt odebírá
warning-already-unsubscribed = Tento server tento projekt neodebírá
warning-not-following = Tento server tohoto tvůrce nesleduje
