# Opsis News Crawler 0.30.0 — Analytics (26/11/2026)

## Ελληνικά

Περιλαμβάνει όλα της 0.29.0 (η μπάρα μένει στη θέση της, ειδησεογραφικά εργαλεία, Χρονικολόγιο χωρίς Opsis). Απαιτεί **.NET 11 Desktop Runtime**. Ως plugin: **Opsis 2.8 ή 3.0**. Οι ρυθμίσεις σας διατηρούνται.

### Νέα (Κέντρο πληροφόρησης → Αναλύσεις)
- Πρόσωπα και οργανισμοί από τους τίτλους, σχέσεις τους (γράφος με τις αποδείξεις), κάλυψη ιστοριών, τάση όρου με ράβδους, παγκόσμιος χάρτης δημοσιευμάτων, ψηφιδωτό ειδήσεων.
- Χάρτης αγορών· μεταβλητότητα, μέγιστη υποχώρηση και σύγκριση με δείκτη· κατανομή χαρτοφυλακίου ανά νόμισμα και χρονικά σταθμισμένη απόδοση· ειδήσεις γύρω από κινήσεις 3%+.
- Ημερολόγιο με κατηγορίες αποτελεσμάτων, μερισμάτων και μακροοικονομικών δεικτών (από το αρχείο JSON που εισάγετε).
- Προαιρετικό τοπικό αρχείο ειδήσεων (30 ημέρες, έως 5.000, κλειστό από προεπιλογή) με αναζήτηση και αλλαγές τίτλων.
- Φίλτρο θέματος· ένδειξη όταν μόνο ορισμένοι αναφέρουν διάψευση.

### Όρια
- Όλα είναι μετρήσεις πάνω στις ειδήσεις και τις αποθηκευμένες τιμές: όχι επαλήθευση, όχι επενδυτική σύσταση. Τα ονόματα και οι χώρες αναγνωρίζονται με απλούς κανόνες κειμένου.
- Η απόδοση TWR χρειάζεται τιμή σε κάθε ημέρα συναλλαγής και δεν υπολογίζεται με split. Οι συγκρίσεις είναι μόνο τιμής (χωρίς μερίσματα).
- Η αναζήτηση αρχείου σαρώνει το αρχείο (όχι ευρετήριο πλήρους κειμένου).
- **Δεν** έχουν γίνει χειροκίνητοι έλεγχοι σε φυσικό Windows 10/11 ή σε ζωντανό Opsis 2.8/3.0· δεν υπάρχει υπογραφή Authenticode.

## English

Includes everything of 0.29.0 (the bar keeps its place, news tools, Chronikologio without Opsis). Needs the **.NET 11 Desktop Runtime**. As a plugin: **Opsis 2.8 or 3.0**. Your settings are kept.

### New (Intelligence centre → Analytics)
- People and organisations from headlines and their relations (graph with evidence), story coverage, a term trend chart, a world map of publications, a news mosaic.
- A market heatmap; volatility, maximum drawdown and benchmark comparison; allocation per currency and time-weighted return; news around moves of 3% or more.
- A calendar with results, dividend and macro categories (from the JSON file you import).
- An optional local news archive (30 days, up to 5,000, off by default) with search and headline changes.
- A topic filter; a hint when only some outlets report a denial.

### Limits
- Everything is counted from the news and stored prices: not verification, not investment advice. Names and countries are recognised by simple text rules.
- The time-weighted return needs a price on every trade day and is not computed with a split. Comparisons are price only (no dividends).
- Archive search scans the archive (no full-text index).
- **Not** manually tested on physical Windows 10/11 or a live Opsis 2.8/3.0; no Authenticode signature.
