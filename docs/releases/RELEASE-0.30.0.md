# Opsis News Crawler 0.30.0 — News intelligence and analytics (26/11/2026)

## Ελληνικά

Μεγάλη έκδοση μετά την 0.28.1. Απαιτεί **.NET 11 Desktop Runtime**. Ως plugin: **Opsis 2.8 ή 3.0** (ίδιο συμβόλαιο plugin 1.0.0.0 με το host). Οι ρυθμίσεις σας διατηρούνται.

### Η μπάρα
- Μένει στη θέση της όταν ανανεώνονται ειδήσεις ή τιμές (χωρίς άλμα προς την αρχή).

### Ειδησεογραφικά εργαλεία (Κέντρο πληροφόρησης)
- Λίστες θεμάτων με σταθερή ταυτότητα και φίλτρο θέματος· αναδυόμενοι όροι.
- Χρονολόγιο ιστορίας· διαφορετικοί αριθμοί, διάψευση από ορισμένους, κοινή πηγή και χώρα ως ενδείξεις για ανθρώπινη κρίση.
- Αναζήτηση, λειτουργία εστίασης, σύγκριση δύο δημοσιευμάτων δίπλα δίπλα.

### Αναλύσεις (Κέντρο πληροφόρησης → Αναλύσεις)
- Πρόσωπα και οργανισμοί, γράφος σχέσεων, κάλυψη ιστοριών, τάση όρου, παγκόσμιος χάρτης δημοσιευμάτων, ψηφιδωτό ειδήσεων.
- Χάρτης αγορών· μεταβλητότητα, μέγιστη υποχώρηση, σύγκριση με δείκτη· κατανομή χαρτοφυλακίου ανά νόμισμα και χρονικά σταθμισμένη απόδοση· ειδήσεις γύρω από κινήσεις 3%+.
- Ημερολόγιο με αποτελέσματα, μερίσματα και μακροοικονομικούς δείκτες (από το αρχείο JSON που εισάγετε).
- Προαιρετικό τοπικό αρχείο ειδήσεων (30 ημέρες, έως 5.000, κλειστό από προεπιλογή) με αναζήτηση και αλλαγές τίτλων.

### Πηγές και διασύνδεση
- Οι τροφοδοσίες χωρίς νέα εδώ και εβδομάδες ζητούνται αραιότερα· στήλη «Κατάσταση» στα διαγνωστικά RSS· η λίστα αναγνωσμένων έχει όριο 5.000.
- **Χρονικολόγιο χωρίς Opsis:** η ροή «Σαν σήμερα» φτάνει στις μπάρες όταν τα δύο προγράμματα τρέχουν χωριστά. Απαιτεί έκδοση του Χρονικολογίου που γράφει το αρχείο (δεν έχει δημοσιευτεί ακόμη).

### Όρια
- Όλα είναι μετρήσεις πάνω στις ειδήσεις και τις αποθηκευμένες τιμές: όχι επαλήθευση, όχι επενδυτική σύσταση. Ονόματα, χώρες και διαφορές αναγνωρίζονται με απλούς κανόνες κειμένου.
- Η απόδοση TWR χρειάζεται τιμή σε κάθε ημέρα συναλλαγής και δεν υπολογίζεται με split· οι συγκρίσεις είναι μόνο τιμής. Η αναζήτηση αρχείου σαρώνει το αρχείο.
- **Δεν** έχουν γίνει χειροκίνητοι έλεγχοι σε φυσικό Windows 10/11, ζωντανή φόρτωση σε Opsis 2.8/3.0 ή δοκιμή μαζί με το Χρονικολόγιο· δεν υπάρχει υπογραφή Authenticode.

## English

A large release after 0.28.1. Needs the **.NET 11 Desktop Runtime**. As a plugin: **Opsis 2.8 or 3.0** (the same plugin contract 1.0.0.0 as the host). Your settings are kept.

### The bar
- Keeps its place when news or prices refresh (no jump back to the beginning).

### News tools (Intelligence centre)
- Topic watchlists with a stable identity and a topic filter; emerging terms.
- Story timeline; differing numbers, a denial reported by only some, shared origin and country as hints for a person to judge.
- Search, a focus reading mode, two reports side by side.

### Analytics (Intelligence centre → Analytics)
- People and organisations, a relation graph, story coverage, term trend, a world map of publications, a news mosaic.
- Market heatmap; volatility, maximum drawdown, benchmark comparison; allocation per currency and time-weighted return; news around moves of 3% or more.
- A calendar with results, dividends and macro indicators (from the JSON file you import).
- An optional local news archive (30 days, up to 5,000, off by default) with search and headline changes.

### Sources and links
- Feeds with no news for weeks are asked less often; a Status column in the RSS diagnostics; the read list is capped at 5,000.
- **Chronikologio without Opsis:** the On this day flow reaches the bars when the two programs run separately. Needs a Chronikologio release that writes the file (not published yet).

### Limits
- Everything is counted from the news and stored prices: not verification, not investment advice. Names, countries and differences come from simple text rules.
- The time-weighted return needs a price on every trade day and is not computed with a split; comparisons are price only. Archive search scans the archive.
- **Not** manually tested on physical Windows 10/11, a live Opsis 2.8/3.0, or together with Chronikologio; no Authenticode signature.
