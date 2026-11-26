# Opsis News Crawler 0.29.0 — News intelligence and source quality (26/11/2026)

## Ελληνικά

Απαιτεί **.NET 11 Desktop Runtime**. Ως plugin: **Opsis 2.8 ή 3.0** (ίδιο συμβόλαιο plugin 1.0.0.0 με το host). Οι ρυθμίσεις σας διατηρούνται.

### Νέα
- **Θέματα:** σταθερή ταυτότητα για κάθε λίστα (διατηρείται σε μετονομασία), φίλτρα (θέμα, πηγή, περιοχή, βαθμός, χρόνος) στη μηχανή αντιστοίχισης.
- **Αναδυόμενοι όροι:** λέξεις που ξαφνικά εμφανίζονται σε τίτλους τουλάχιστον τριών εκδοτών· μέτρο κάλυψης, όχι αλήθειας.
- **Ιστορίες:** χρονολόγιο με τις πραγματικές ώρες δημοσίευσης, διαφορετικοί αριθμοί μεταξύ εκδοτών και κοινή πηγή (πρακτορείο ή ίδιο κείμενο) ως ενδείξεις για ανθρώπινη κρίση, χώρα που αναφέρεται (άγνωστη όταν δεν είναι σαφής).
- **Αναζήτηση** στις ειδήσεις της τρέχουσας συνεδρίας, **λειτουργία εστίασης** (πληκτρολόγιο, υψηλή αντίθεση) και **σύγκριση δύο δημοσιευμάτων** δίπλα δίπλα.
- **Πηγές:** οι τροφοδοσίες χωρίς νέα άρθρα εδώ και εβδομάδες ζητούνται αραιότερα (έως 1 ώρα)· στήλη «Κατάσταση» στα διαγνωστικά RSS· η λίστα αναγνωσμένων έχει όριο 5.000.
- **Χρονικολόγιο χωρίς Opsis:** οι γραμμές «Σαν σήμερα» φτάνουν στις μπάρες και όταν τα δύο προγράμματα τρέχουν ξεχωριστά (αρχείο `%LocalAppData%\Opsis\feeds\san-simera.json`). Απαιτεί έκδοση του Χρονικολογίου που γράφει το αρχείο.

### Όρια
- Η αναζήτηση καλύπτει μόνο τις ειδήσεις που κρατά το πρόγραμμα· δεν υπάρχει ακόμη ευρετήριο εκτός σύνδεσης.
- Οι ενδείξεις διαφορών/κοινής πηγής/χώρας είναι απλοί κανόνες πάνω στους τίτλους και τις περιλήψεις RSS.
- **Δεν** έχουν δοκιμαστεί χειροκίνητα σε φυσικό Windows 10/11, ούτε σε ζωντανό Opsis 2.8/3.0, ούτε τα δύο προγράμματα μαζί για τη ροή του Χρονικολογίου. Δεν υπάρχει υπογραφή Authenticode.

## English

Needs the **.NET 11 Desktop Runtime**. As a plugin: **Opsis 2.8 or 3.0** (the same plugin contract 1.0.0.0 as the host). Your settings are kept.

### New
- **Topics:** a stable identity for each watchlist (survives renames) and filters (topic, source, region, score, time) in the matcher.
- **Emerging terms:** words that suddenly appear in headlines of at least three publishers; a measure of coverage, not of truth.
- **Stories:** a timeline with real publication times; numbers that differ between publishers and a shared origin (agency or identical text) as hints for a person to judge; the country a story names (unknown when not clear).
- **Search** over the current news, a **focus reading mode** (keyboard, high contrast) and **two reports side by side**.
- **Sources:** feeds with no new story for weeks are asked less often (at most 1 hour); a Status column in the RSS diagnostics; the read list is capped at 5,000.
- **Chronikologio without Opsis:** the On this day lines reach the bars when the two programs run separately (`%LocalAppData%\Opsis\feeds\san-simera.json`). Needs a Chronikologio build that writes the file.

### Limits
- Search covers only the news the program holds; there is no offline index yet.
- The difference, origin and country hints are simple rules over RSS titles and summaries.
- **Not** manually tested on physical Windows 10/11, nor in a live Opsis 2.8/3.0, nor the two programs together for the Chronikologio flow. No Authenticode signature.
