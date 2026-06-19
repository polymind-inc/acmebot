<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { Activity, AlertTriangle, BadgeCheck, CircleSlash, ExternalLink, Info, PowerOff, Shield, ShieldAlert, ShieldCheck, X } from 'lucide-vue-next';

import { formatApiError, getCertificateRenewals, getCertificates, getDnsZones, issueCertificate, renewCertificate, revokeCertificate } from '@/api/acmebotApi';
import { getLatestRelease } from '@/api/releases';
import type { CertificateItem, CertificatePolicyItem, CertificateRenewalItem, DnsZoneGroup, ReleaseInfo } from '@/api/types';
import AddCertificateDialog from '@/components/AddCertificateDialog.vue';
import CertificateTable from '@/components/CertificateTable.vue';
import ConfirmRevokeDialog from '@/components/ConfirmRevokeDialog.vue';
import DetailsDrawer from '@/components/DetailsDrawer.vue';
import OperationOverlay from '@/components/OperationOverlay.vue';
import ToastStack, { type ToastMessage } from '@/components/ToastStack.vue';
import { getCertificateCategory, getCertificateStatus } from '@/utils/certificates';
import { isNewerVersion, isVersionLike } from '@/utils/versions';

const certificates = ref<CertificateItem[]>([]);
const dnsZoneGroups = ref<DnsZoneGroup[]>([]);
const renewals = ref<CertificateRenewalItem[]>([]);
const selectedCertificate = ref<CertificateItem | null>(null);
const pendingRevokeCertificate = ref<CertificateItem | null>(null);
const addDialogOpen = ref(false);
const detailsBusy = ref(false);
const toasts = ref<ToastMessage[]>([]);
const upgradeNotice = ref<ReleaseInfo | null>(null);
let toastId = 0;
const acmebotVersion = __ACMEBOT_VERSION__;
const acmebotCommitHash = __ACMEBOT_COMMIT_HASH__;
const acmebotRepositoryUrl = 'https://github.com/polymind-inc/acmebot';
const dismissedUpgradeStorageKey = 'acmebot.dismissedUpgradeVersion';

const certificateState = reactive({
  loading: false,
  error: '',
});

const dnsZoneState = reactive({
  loading: false,
});

const renewalState = reactive({
  loading: false,
  error: '',
});

const operation = reactive({
  active: false,
  title: '',
  message: '',
});

const summary = computed(() => {
  const statusCounts = certificates.value.reduce(
    (counts, certificate) => {
      const status = getCertificateStatus(certificate);
      counts[status.kind] += 1;
      return counts;
    },
    { valid: 0, warning: 0, expired: 0, disabled: 0 },
  );

  return {
    total: certificates.value.length,
    managed: certificates.value.filter((certificate) => getCertificateCategory(certificate) === 'managed').length,
    otherCa: certificates.value.filter((certificate) => getCertificateCategory(certificate) === 'other-ca').length,
    unmanaged: certificates.value.filter((certificate) => getCertificateCategory(certificate) === 'unmanaged').length,
    ...statusCounts,
  };
});

const summaryItems = computed(() => [
  { label: 'Managed', value: summary.value.managed, tone: 'good', icon: ShieldCheck },
  { label: 'Expiring soon', value: summary.value.warning, tone: 'warning', icon: ShieldAlert },
  { label: 'Expired', value: summary.value.expired, tone: 'danger', icon: AlertTriangle },
  { label: 'Disabled', value: summary.value.disabled, tone: 'disabled', icon: PowerOff },
  { label: 'Other CA', value: summary.value.otherCa, tone: 'neutral', icon: BadgeCheck },
  { label: 'Unmanaged', value: summary.value.unmanaged, tone: 'neutral', icon: CircleSlash },
]);

const acmebotCommitLabel = computed(() => (acmebotCommitHash.length > 7 ? acmebotCommitHash.slice(0, 7) : acmebotCommitHash));
const acmebotVersionUrl = computed(() => (isVersionLike(acmebotVersion) ? `${acmebotRepositoryUrl}/releases/tag/${encodeURIComponent(acmebotVersion)}` : null));
const acmebotCommitUrl = computed(() => (isCommitHashLike(acmebotCommitHash) ? `${acmebotRepositoryUrl}/commit/${encodeURIComponent(acmebotCommitHash)}` : null));
const renewalByCertificateName = computed(() => new Map(renewals.value.map((renewal) => [renewal.certificateName, renewal])));
const selectedCertificateRenewal = computed(() => (selectedCertificate.value ? renewalByCertificateName.value.get(selectedCertificate.value.name) ?? null : null));
const renewalAttentionCount = computed(() => renewals.value.filter((renewal) => renewal.statusKind === 'attention').length);

onMounted(async () => {
  void loadUpgradeNotice();
  await Promise.all([loadCertificates(), loadRenewals()]);
});

async function loadUpgradeNotice(): Promise<void> {
  if (!isVersionLike(acmebotVersion)) {
    return;
  }

  try {
    const latestRelease = await getLatestRelease();

    if (!latestRelease || !isNewerVersion(latestRelease.version, acmebotVersion)) {
      return;
    }

    if (getDismissedUpgradeVersion() === latestRelease.version) {
      return;
    }

    upgradeNotice.value = latestRelease;
  } catch {
    return;
  }
}

function getDismissedUpgradeVersion(): string | null {
  try {
    return window.localStorage.getItem(dismissedUpgradeStorageKey);
  } catch {
    return null;
  }
}

function rememberDismissedUpgradeVersion(version: string): void {
  try {
    window.localStorage.setItem(dismissedUpgradeStorageKey, version);
  } catch {
    return;
  }
}

function dismissUpgradeNotice(): void {
  if (upgradeNotice.value) {
    rememberDismissedUpgradeVersion(upgradeNotice.value.version);
  }

  upgradeNotice.value = null;
}

function isCommitHashLike(value: string): boolean {
  return /^[0-9a-f]{7,40}$/i.test(value.trim());
}

function pushToast(type: ToastMessage['type'], title: string, message: string): void {
  const id = ++toastId;
  toasts.value = [...toasts.value, { id, type, title, message }];
  window.setTimeout(() => dismissToast(id), 6000);
}

function dismissToast(id: number): void {
  toasts.value = toasts.value.filter((message) => message.id !== id);
}

async function loadCertificates(): Promise<void> {
  certificateState.loading = true;
  certificateState.error = '';

  try {
    certificates.value = await getCertificates();
  } catch (error) {
    certificateState.error = formatApiError(error);
    pushToast('error', 'Failed to load certificates', certificateState.error);
  } finally {
    certificateState.loading = false;
  }
}

async function loadDnsZones(): Promise<void> {
  if (dnsZoneState.loading) {
    return;
  }

  dnsZoneState.loading = true;

  try {
    dnsZoneGroups.value = await getDnsZones();
  } catch (error) {
    pushToast('error', 'Failed to load DNS zones', formatApiError(error));
  } finally {
    dnsZoneState.loading = false;
  }
}

async function loadRenewals(): Promise<void> {
  renewalState.loading = true;
  renewalState.error = '';

  try {
    renewals.value = await getCertificateRenewals();
  } catch (error) {
    renewalState.error = formatApiError(error);
    pushToast('error', 'Failed to load renewals', renewalState.error);
  } finally {
    renewalState.loading = false;
  }
}

async function refreshCertificates(): Promise<void> {
  await Promise.all([loadCertificates(), loadRenewals()]);
}

async function runCertificateOperation(title: string, message: string, action: () => Promise<void>): Promise<void> {
  operation.active = true;
  operation.title = title;
  operation.message = message;

  try {
    await action();
    pushToast('success', 'Operation completed', message.replace('...', ' completed.'));
    addDialogOpen.value = false;
    selectedCertificate.value = null;
    await refreshCertificates();
  } catch (error) {
    pushToast('error', 'Operation failed', formatApiError(error));
  } finally {
    operation.active = false;
  }
}

async function handleIssueCertificate(policy: CertificatePolicyItem): Promise<void> {
  await runCertificateOperation('Issuing certificate', 'Issuing certificate...', () => issueCertificate(policy));
}

async function handleRenewCertificate(certificate: CertificateItem): Promise<void> {
  detailsBusy.value = true;

  try {
    await runCertificateOperation('Renewing certificate', `Renewing ${certificate.name}...`, () => renewCertificate(certificate.name));
  } finally {
    detailsBusy.value = false;
  }
}

async function handleRevokeCertificate(certificate: CertificateItem): Promise<void> {
  pendingRevokeCertificate.value = certificate;
}

async function handleCopy(label: string, value: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(value);
    pushToast('success', `${label} copied`, 'Copied to clipboard.');
  } catch {
    pushToast('error', 'Copy failed', `Could not copy ${label.toLowerCase()}.`);
  }
}

async function confirmRevokeCertificate(): Promise<void> {
  if (!pendingRevokeCertificate.value) {
    return;
  }

  const certificate = pendingRevokeCertificate.value;

  detailsBusy.value = true;

  try {
    await runCertificateOperation('Revoking certificate', `Revoking ${certificate.name}...`, () => revokeCertificate(certificate.name));
  } finally {
    detailsBusy.value = false;
    pendingRevokeCertificate.value = null;
  }
}
</script>

<template>
  <div class="app-shell">
    <header class="app-header">
      <div class="app-header__inner">
        <div class="brand-lockup">
          <div
            class="brand-mark"
            aria-hidden="true"
          >
            <Shield :size="21" />
          </div>
          <div>
            <div class="brand-title">
              Acmebot
            </div>
            <div class="brand-subtitle">
              Certificate automation
            </div>
          </div>
        </div>
        <div class="header-status">
          <Activity
            :size="16"
            aria-hidden="true"
          />
          <span>{{ summary.total }} certificates</span>
        </div>
      </div>
    </header>

    <main class="app-main">
      <h1
        id="page-heading"
        class="visually-hidden"
      >
        Certificate Operations
      </h1>

      <div
        v-if="upgradeNotice"
        class="banner banner--upgrade"
        role="status"
      >
        <Info
          :size="17"
          aria-hidden="true"
        />
        <div class="banner__body">
          <span class="banner__title">Acmebot {{ upgradeNotice.version }} is available</span>
          <span class="banner__message">This Acmebot installation is running {{ acmebotVersion }}.</span>
        </div>
        <a
          class="secondary-button banner__action"
          :href="upgradeNotice.releaseUrl"
          target="_blank"
          rel="noopener noreferrer"
        >
          <ExternalLink
            :size="15"
            aria-hidden="true"
          />
          Release notes
        </a>
        <button
          class="icon-only-button banner__dismiss"
          type="button"
          title="Dismiss upgrade notification"
          @click="dismissUpgradeNotice"
        >
          <X
            :size="16"
            aria-hidden="true"
          />
        </button>
      </div>

      <section
        class="summary-grid"
        aria-label="Certificate summary"
      >
        <div
          v-for="item in summaryItems"
          :key="item.label"
          class="summary-item"
          :class="`summary-item--${item.tone}`"
        >
          <component
            :is="item.icon"
            :size="18"
            aria-hidden="true"
          />
          <div>
            <div class="summary-item__value">
              {{ item.value }}
            </div>
            <div class="summary-item__label">
              {{ item.label }}
            </div>
          </div>
        </div>
      </section>

      <div
        v-if="certificateState.error"
        class="banner banner--error"
        role="alert"
      >
        <AlertTriangle
          :size="17"
          aria-hidden="true"
        />
        <span>{{ certificateState.error }}</span>
      </div>

      <div
        v-if="renewalState.error"
        class="banner banner--error"
        role="alert"
      >
        <AlertTriangle
          :size="17"
          aria-hidden="true"
        />
        <span>{{ renewalState.error }}</span>
      </div>

      <div
        v-else-if="renewalAttentionCount > 0"
        class="banner banner--warning"
        role="status"
      >
        <AlertTriangle
          :size="17"
          aria-hidden="true"
        />
        <span>{{ renewalAttentionCount }} automatic {{ renewalAttentionCount === 1 ? 'renewal needs' : 'renewals need' }} attention.</span>
      </div>

      <CertificateTable
        :certificates="certificates"
        :dns-zone-groups="dnsZoneGroups"
        :loading="certificateState.loading"
        :selected-certificate="selectedCertificate"
        :renewals="renewals"
        :renewals-loading="renewalState.loading"
        @select="selectedCertificate = $event"
        @refresh="refreshCertificates"
        @add="addDialogOpen = true"
      />
    </main>

    <footer
      class="app-footer"
      aria-label="Acmebot build metadata"
    >
      <div class="app-footer__inner">
        <span>Acmebot</span>
        <dl class="build-metadata">
          <div class="build-metadata__item">
            <dt>Version</dt>
            <dd>
              <a
                v-if="acmebotVersionUrl"
                class="build-metadata__link"
                :href="acmebotVersionUrl"
                target="_blank"
                rel="noopener noreferrer"
              >{{ acmebotVersion }}</a>
              <span v-else>{{ acmebotVersion }}</span>
            </dd>
          </div>
          <div class="build-metadata__item">
            <dt>Commit</dt>
            <dd>
              <a
                v-if="acmebotCommitUrl"
                class="build-metadata__link"
                :href="acmebotCommitUrl"
                :title="acmebotCommitHash"
                target="_blank"
                rel="noopener noreferrer"
              >{{ acmebotCommitLabel }}</a>
              <span
                v-else
                :title="acmebotCommitHash"
              >{{ acmebotCommitLabel }}</span>
            </dd>
          </div>
        </dl>
      </div>
    </footer>

    <AddCertificateDialog
      :open="addDialogOpen"
      :zones="dnsZoneGroups"
      :loading-zones="dnsZoneState.loading"
      :sending="operation.active"
      @close="addDialogOpen = false"
      @load-zones="loadDnsZones"
      @submit="handleIssueCertificate"
    />

    <DetailsDrawer
      :open="selectedCertificate !== null"
      :certificate="selectedCertificate"
      :busy="detailsBusy || operation.active"
      :renewal="selectedCertificateRenewal"
      :renewal-loading="renewalState.loading"
      @close="selectedCertificate = null"
      @renew="handleRenewCertificate"
      @revoke="handleRevokeCertificate"
      @copy="handleCopy"
    />

    <ConfirmRevokeDialog
      :open="pendingRevokeCertificate !== null"
      :certificate-name="pendingRevokeCertificate?.name ?? ''"
      :busy="detailsBusy || operation.active"
      @cancel="pendingRevokeCertificate = null"
      @confirm="confirmRevokeCertificate"
    />

    <OperationOverlay
      :active="operation.active"
      :title="operation.title"
      :message="operation.message"
    />
    <ToastStack
      :messages="toasts"
      @dismiss="dismissToast"
    />
  </div>
</template>
