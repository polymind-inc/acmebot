<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { Activity, AlertTriangle, BadgeCheck, CircleSlash, Shield, ShieldAlert, ShieldCheck } from 'lucide-vue-next';

import { formatApiError, getCertificates, getDnsZones, issueCertificate, renewCertificate, revokeCertificate } from '@/api/acmebotApi';
import type { CertificateItem, CertificatePolicyItem, DnsZoneGroup } from '@/api/types';
import AddCertificateDialog from '@/components/AddCertificateDialog.vue';
import CertificateTable from '@/components/CertificateTable.vue';
import ConfirmRevokeDialog from '@/components/ConfirmRevokeDialog.vue';
import DetailsDrawer from '@/components/DetailsDrawer.vue';
import OperationOverlay from '@/components/OperationOverlay.vue';
import ToastStack, { type ToastMessage } from '@/components/ToastStack.vue';
import { getCertificateCategory, getCertificateStatus } from '@/utils/certificates';

const certificates = ref<CertificateItem[]>([]);
const dnsZoneGroups = ref<DnsZoneGroup[]>([]);
const selectedCertificate = ref<CertificateItem | null>(null);
const pendingRevokeCertificate = ref<CertificateItem | null>(null);
const addDialogOpen = ref(false);
const detailsBusy = ref(false);
const toasts = ref<ToastMessage[]>([]);
let toastId = 0;
const dashboardVersion = __ACMEBOT_DASHBOARD_VERSION__;
const dashboardCommitHash = __ACMEBOT_DASHBOARD_COMMIT_HASH__;

const certificateState = reactive({
  loading: false,
  error: '',
});

const dnsZoneState = reactive({
  loading: false,
  loaded: false,
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
    { valid: 0, warning: 0, expired: 0 },
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
  { label: 'Other CA', value: summary.value.otherCa, tone: 'neutral', icon: BadgeCheck },
  { label: 'Unmanaged', value: summary.value.unmanaged, tone: 'neutral', icon: CircleSlash },
]);

const dashboardCommitLabel = computed(() => (dashboardCommitHash.length > 7 ? dashboardCommitHash.slice(0, 7) : dashboardCommitHash));

onMounted(async () => {
  await loadCertificates();
  await loadDnsZones(false);
});

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

async function loadDnsZones(showErrorToast = true): Promise<void> {
  if (dnsZoneState.loaded || dnsZoneState.loading) {
    return;
  }

  dnsZoneState.loading = true;

  try {
    dnsZoneGroups.value = await getDnsZones();
    dnsZoneState.loaded = true;
  } catch (error) {
    if (showErrorToast) {
      pushToast('error', 'Failed to load DNS zones', formatApiError(error));
    }
  } finally {
    dnsZoneState.loading = false;
  }
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
    await loadCertificates();
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

      <CertificateTable
        :certificates="certificates"
        :dns-zone-groups="dnsZoneGroups"
        :loading="certificateState.loading"
        :selected-certificate="selectedCertificate"
        @select="selectedCertificate = $event"
        @refresh="loadCertificates"
        @add="addDialogOpen = true"
      />
    </main>

    <footer
      class="app-footer"
      aria-label="Dashboard build metadata"
    >
      <div class="app-footer__inner">
        <span>Acmebot Dashboard</span>
        <dl class="build-metadata">
          <div class="build-metadata__item">
            <dt>Version</dt>
            <dd>{{ dashboardVersion }}</dd>
          </div>
          <div class="build-metadata__item">
            <dt>Commit</dt>
            <dd :title="dashboardCommitHash">
              {{ dashboardCommitLabel }}
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
