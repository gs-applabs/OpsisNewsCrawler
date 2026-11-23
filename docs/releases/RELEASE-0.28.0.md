# Opsis News Crawler 0.28.0 — P0 + P1 + P2

## Ελληνικά

**Έκδοση εφαρμογής:** 0.28.0. **Crawler plugin DLL AssemblyVersion και FileVersion:** 1.0.0.0.
**Κοινή DLL συμβολαίου με Opsis/Χρονικολόγιο:** 0.8.0.0, διατηρείται για συμβατότητα ονόματος. **Προσοχή:** η κοινή DLL περιέχει νέες προαιρετικές κλάσεις στο P1, επομένως η ίδια φυσική έκδοση του αρχείου πρέπει να εγκαθίσταται στον host πριν ενεργοποιηθεί το νέο plugin.

### Βελτιώσεις P0
- Αξιόπιστες λήψεις RSS με όρια ταυτόχρονων αιτημάτων, πραγματικό περιορισμό bytes, backoff και αναλυτικά health metrics.
- Περιορισμός εγγραφών στον SSD, έλεγχοι GDI, παρακολούθηση επιδόσεων, καλύτεροι μηχανισμοί ανανέωσης και τερματισμού.
- Διατήρηση rich feed metadata σε plugins χωρίς αφαίρεση παλαιών interfaces.

### Βελτιώσεις P1
- Live Video Hub με επιλογή προκαθορισμένων από τον χρήστη καναλιών, κατάταξη βάσει γεγονότος, muted YouTube embeds και χειροκίνητη αλλαγή.
- Βαθμολογία έκτακτων ειδήσεων με εξηγήσεις, συνδυασμό διαφορετικών domains της ίδιας συντακτικής ομάδας, έλεγχο RSS ημερομηνιών.
- Πέντε προαιρετικά widgets Opsis: ειδήσεις, αγορές, καιρός, έκτακτες, ιστορικά γεγονότα.
- Κοινό τοπικό event bus, κοινή οπτική ταυτότητα Opsis UI, keyboard accessibility, DPI-aware popup, smart contrast, βελτιωμένη σχεδίαση, energy/metered mode και dashboards.

### Νέες λειτουργίες P2
- **Watchlists θεμάτων:** αποθηκευμένες τοπικές λέξεις-κλειδιά, προαιρετικός γεωγραφικός δείκτης και ενεργοποίηση/απενεργοποίηση από νέα σελίδα ρυθμίσεων.
- **Story explorer:** ομαδοποίηση δημοσιευμάτων ανά σχετικό γεγονός, καταμέτρηση πηγών με εκδοτική ομαδοποίηση.
- **Τοπικό timeline:** ειδήσεις και στοιχεία των συνδεδεμένων εφαρμογών μαζί, με χωριστό ιστορικό έτος.
- **Ημερήσια εξαγωγή HTML:** χωρίς αίτημα δικτύου, καθαρισμός HTML περιεχομένου, ασφαλή HTTPS links, δυνατότητα εκτύπωσης σε PDF από τον browser.
- **Προαιρετικό ledger χαρτοφυλακίου:** αγορές, πωλήσεις, μερίσματα, split, προμήθειες, μέσο σταθμικό κόστος και πραγματοποιημένο P/L. Δεν υποκαθιστά φορολογικό/επενδυτικό λογισμικό.
- **Ειδοποιήσεις ποσοστιαίας ημερήσιας μεταβολής:** νέα επιλογή `Metric=changePercent`, παράλληλα με τα παλιά price alerts.
- **Νέο P2 Insights Center** μέσα στην υπάρχουσα οικογένεια UI του Opsis. Δεν απαιτεί νέα προγράμματα/αυτόνομες υπηρεσίες.

### Ασφάλεια και όρια
- **Η εγκατάσταση ως plugin στο Opsis απαιτεί συντονισμένη αναβάθμιση του host και του Χρονικολογίου**, ώστε να μοιράζονται το ίδιο ακριβώς DLL συμβολαίου 0.8.0.0. Μην αναμειγνύετε διαφορετικές εκδόσεις αρχείου μόνο επειδή έχουν ίδιο AssemblyVersion.
- Το P2 δεν κάνει ανεξάρτητη fact checking πιστοποίηση: η συγκέντρωση πηγών και το score δεν αποδεικνύουν αλήθεια ή εκδοτική ανεξαρτησία.
- Όλα τα δεδομένα και εξαγωγές P2 είναι τοπικά. Η HTML εξαγωγή ενεργοποιείται χειροκίνητα.
- Δεν προστίθενται νέοι συλλέκτες μη επαληθευμένων ή αδειοδοτημένων video streams.
- Δεν δηλώνεται ότι έχουν ολοκληρωθεί δοκιμές σε φυσικούς υπολογιστές ή υπογραφή Authenticode χωρίς αντίστοιχη τεκμηρίωση.
- Τα ZIP/installer και οι SHA-256 δημιουργούνται από Windows GitHub Actions του release branch και διατίθενται στα workflow artifacts εφόσον επιτύχουν.

## English

Opsis News Crawler 0.28.0 packages P0 stability, P1 video/host UI, and P2 local intelligence features: private topic watchlists, source clustering, a merged suite timeline, safe offline HTML digests, optional portfolio transaction accounting, and percent-change price alerts. The crawler plugin DLL has assembly/file identity **1.0.0.0**, while the shared Opsis contract retains identity **0.8.0.0**. Cross-app deployment requires the **same binary** contract on the host and all plugins. Installer/portable/source files and SHA-256 hashes are generated and tested in the Windows release workflow. No claims of verified streaming rights, real-PC GPU tests or Authenticode signing.
