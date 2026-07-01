v1.4.0
- When labels are supplied and a Password Secret Location Suffix is entered for the certificate store, apply labels to both the certificate and the password secrets

v1.3.0
- Bug Fix: Certificates with encrypted private keys utilizing user managed replication were having their passwords stored separately but in the global scope instead of the replicated regions where the certificate was stored.
- Re-ordered adding tags to before adding secret versions to make sure any auth rules based on tags are supported.

v1.2.0
- Added support for labels
- Added support for setting ttl duration
- Added support for setting version destroy ttl duration
- Added support for setting replication regions
- Bug Fix: Modified logic to obtain organization from project to allow for recursive folder ownership between these layers 

v1.1.0
- Added new Entry Parameter of Tags to support the assignment of one-to-many organization tags to a certificate/secret being added as a new certificate/secret

v1.0.0
- Initial Version
