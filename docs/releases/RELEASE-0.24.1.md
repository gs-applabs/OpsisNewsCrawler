# Opsis News Crawler 0.24.1

## Ελληνικά
- **Δεύτερη πηγή καιρού: MET Norway** (δωρεάν, χωρίς κλειδί, CC BY 4.0). Το Open-Meteo μένει πρώτη πηγή· όσα μέρη δεν απαντά (ή όλα, όταν είναι εκτός λειτουργίας) τα ζητά από το MET Norway, το πολύ 8 ανά ανανέωση, με σεβασμό στο `Expires`.
- **Προαιρετικό «Πρόσθεσε την κατά προσέγγιση θέση μου»** (Ρυθμίσεις > Καιρός): ρωτά τα Windows μία φορά, μετά από επιβεβαίωση· αποθηκεύεται μόνο σημείο στρογγυλεμένο στο ~1 km.
- **Τα εικονίδια (favicons) των πηγών εμφανίζονται ξανά:** από την 0.22.0 το μοτίβο που βρίσκει το `<link rel="icon">` είχε χαλάσει και δεν ταίριαζε ποτέ. Νέο τεστ.
- **Ασφάλεια:** τα workflows του CI τρέχουν με token μόνο ανάγνωσης· τα αρχεία Leaflet στις σελίδες χάρτη έχουν έλεγχο ακεραιότητας (SRI).
- Ο λογαριασμός GitHub μετονομάστηκε σε `gs-applabs`· οι έλεγχοι εμπιστοσύνης των ενημερώσεων δέχονται και το νέο και το παλιό όνομα.
- Για το Opsis 2.4.6.x (plugin contract 0.8.0.0). Τεστ: 566 (Core) + 34 (Plugin), όλα επιτυχή· e2e εγκατάστασης σε runner του GitHub επιτυχές.
- ΔΕΝ ΕΠΑΛΗΘΕΥΤΗΚΕ σε πραγματικό PC: η ερώτηση άδειας θέσης των Windows, η άρνησή της και οι υπηρεσίες θέσης απενεργοποιημένες.

## English
- **Second weather source: MET Norway** (free, no key, CC BY 4.0). Open-Meteo stays the first source; places it does not answer for (or all of them when it is down) are asked from MET Norway, at most 8 per refresh, honouring `Expires`.
- **Opt-in "Add my approximate location"** (Settings > Weather): asks Windows once after a confirmation; only a point rounded to about 1 km is stored.
- **Source icons (favicons) show again:** since 0.22.0 the pattern that finds `<link rel="icon">` was broken and never matched. New test.
- **Security:** CI workflows run with a read-only token; the Leaflet files on the map pages carry SRI hashes.
- The GitHub account was renamed to `gs-applabs`; the trusted-URL checks of updates accept both the new and the old name.
- For Opsis 2.4.6.x (plugin contract 0.8.0.0). Tests: 566 (Core) + 34 (Plugin), all passed; installer end-to-end on a GitHub runner passed.
- NOT VERIFIED on a real PC: the Windows location permission prompt, a refusal, and Location services switched off.
