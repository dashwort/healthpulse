# Delivery pipeline

CI uses credential-free hosted build/test runners. Production admission requires
two independent agent reviews, exact source/artifact identities, staging, database
and payload compatibility, backups and tested rollback. Only the privileged
deployment broker may install a release.

This draft adds baseline CI only. It does not certify currently deployed dirty
source, private compiler checks, packaged UI acceptance or game runtime. Preserve
all active workspaces and recovery artifacts.

The direct sudo deployment job is removed. Read-only PR image builds are separate
from main-only publication of the exact tested image archive. Production admission
intentionally fails closed until the independently verified broker is ready.
