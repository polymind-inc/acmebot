<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { CirclePlus, KeyRound, ShieldPlus, Tag, X } from 'lucide-vue-next';

import type { CertificatePolicyItem, DnsZoneGroup, KeyCurveName, KeyType, SelectableDnsZone } from '@/api/types';
import { displayDnsName } from '@/utils/certificates';
import { createCertificateName, createDelegatedDnsAlias } from '@/utils/dnsNames';

import AdvancedCertificateOptions from './AdvancedCertificateOptions.vue';
import DelegatedDnsNameEditor from './DelegatedDnsNameEditor.vue';
import ManagedDnsNameEditor from './ManagedDnsNameEditor.vue';
import SearchableZoneSelect from './SearchableZoneSelect.vue';

const props = defineProps<{
  open: boolean;
  zones: DnsZoneGroup[];
  loadingZones: boolean;
  sending: boolean;
}>();

const emit = defineEmits<{
  close: [];
  submit: [policy: CertificatePolicyItem];
  'load-zones': [];
}>();

interface CertificateTagInput {
  id: number;
  key: string;
  value: string;
}

interface TagValidationOutcome {
  tags: Record<string, string>;
  message: string;
}

const issueModeOptions = {
  managed: {
    editor: ManagedDnsNameEditor,
    zoneLabel: 'DNS zone',
    zoneStepLabel: 'DNS zone',
    emptyStatusLabel: 'Select zone',
    useSelectedZoneProvider: false,
    useGeneratedDnsAlias: false,
  },
  delegated: {
    editor: DelegatedDnsNameEditor,
    zoneLabel: 'CNAME zone',
    zoneStepLabel: 'CNAME zone',
    emptyStatusLabel: 'Select zone',
    useSelectedZoneProvider: true,
    useGeneratedDnsAlias: true,
  },
} as const;

type IssueMode = keyof typeof issueModeOptions;

const selectedZone = ref<SelectableDnsZone | null>(null);

const validKeySizes = [2048, 3072, 4096];
const validKeyCurves: KeyCurveName[] = ['P-256', 'P-384', 'P-521', 'P-256K'];

const form = reactive({
  issueMode: 'managed' as IssueMode,
  dnsNames: [] as string[],
  dnsProviderName: '',
  useAdvancedOptions: false,
  certificateName: '',
  keyType: 'RSA' as KeyType,
  keySize: 2048,
  keyCurveName: 'P-256' as KeyCurveName,
  reuseKey: false,
  profile: '',
  tags: [] as CertificateTagInput[],
});

const activeIssueMode = computed(() => issueModeOptions[form.issueMode]);
const dnsNameEditorComponent = computed(() => activeIssueMode.value.editor);
const customCertificateName = computed(() => form.certificateName.trim());
const defaultCertificateName = computed(() => (form.dnsNames.length > 0 ? createCertificateName(form.dnsNames[0]) : ''));
const effectiveCertificateName = computed(() => (form.useAdvancedOptions && customCertificateName.value ? customCertificateName.value : defaultCertificateName.value));
const certificateNameError = computed(() => (effectiveCertificateName.value ? validateCertificateName(effectiveCertificateName.value) : ''));
const keyOptionError = computed(() => validateKeyOptions());
const tagValidation = computed(() => (form.useAdvancedOptions ? validateTags(form.tags) : { tags: {}, message: '' }));
const tagError = computed(() => tagValidation.value.message);
const zoneLabel = computed(() => activeIssueMode.value.zoneLabel);
const zoneStepLabel = computed(() => activeIssueMode.value.zoneStepLabel);
const delegatedDnsAlias = computed(() => (activeIssueMode.value.useGeneratedDnsAlias && selectedZone.value ? createDelegatedDnsAlias(form.dnsNames, selectedZone.value) : ''));
const submitValidationMessage = computed(() => {
  if (form.dnsNames.length === 0) {
    return 'Add at least one DNS name.';
  }

  return certificateNameError.value || keyOptionError.value || tagError.value;
});
const canSubmit = computed(() => !props.sending && submitValidationMessage.value === '');
const keySummary = computed(() => (form.keyType === 'RSA' ? `${form.keySize} bit RSA` : `${form.keyCurveName} EC`));
const issueStatusLabel = computed(() => {
  if (form.dnsNames.length > 0) {
    return 'Ready';
  }

  if (selectedZone.value) {
    return 'Add DNS name';
  }

  return activeIssueMode.value.emptyStatusLabel;
});
const dnsNamesSummary = computed(() => {
  if (form.dnsNames.length === 0) {
    return 'None added';
  }

  const firstDnsName = displayDnsName(form.dnsNames[0]);

  if (form.dnsNames.length === 1) {
    return firstDnsName;
  }

  return `${firstDnsName} +${form.dnsNames.length - 1}`;
});
const dnsNameCountLabel = computed(() => `${form.dnsNames.length} ${form.dnsNames.length === 1 ? 'DNS name' : 'DNS names'}`);
const tagCount = computed(() => Object.keys(tagValidation.value.tags).length);
const tagCountLabel = computed(() => (tagCount.value === 0 ? 'No tags' : `${tagCount.value} ${tagCount.value === 1 ? 'tag' : 'tags'}`));

watch(
  () => props.open,
  (open) => {
    if (open) {
      resetForm();
      emit('load-zones');
    }
  },
);

watch(
  () => form.issueMode,
  () => {
    form.dnsNames = [];
    form.dnsProviderName = activeIssueMode.value.useSelectedZoneProvider && selectedZone.value ? selectedZone.value.dnsProviderName : '';
  },
);

watch(
  selectedZone,
  (zone) => {
    if (activeIssueMode.value.useSelectedZoneProvider) {
      form.dnsProviderName = zone?.dnsProviderName ?? '';
    } else if (form.dnsNames.length === 0) {
      form.dnsProviderName = '';
    }
  },
);

watch(
  () => form.useAdvancedOptions,
  (useAdvancedOptions) => {
    if (!useAdvancedOptions) {
      form.certificateName = '';
      form.keyType = 'RSA';
      form.keySize = 2048;
      form.keyCurveName = 'P-256';
      form.reuseKey = false;
      form.profile = '';
      form.tags = [];
    }
  },
);

function resetForm(): void {
  selectedZone.value = null;
  form.issueMode = 'managed';
  form.dnsNames = [];
  form.dnsProviderName = '';
  form.useAdvancedOptions = false;
  form.certificateName = '';
  form.keyType = 'RSA';
  form.keySize = 2048;
  form.keyCurveName = 'P-256';
  form.reuseKey = false;
  form.profile = '';
  form.tags = [];
}

function validateCertificateName(certificateName: string): string {
  const trimmedCertificateName = certificateName.trim();

  if (!trimmedCertificateName) {
    return '';
  }

  if (trimmedCertificateName.length > 127) {
    return 'Certificate Name must be 127 characters or fewer.';
  }

  if (!/^[0-9a-zA-Z-]+$/.test(trimmedCertificateName)) {
    return 'Certificate Name can contain only letters, numbers, and hyphens.';
  }

  return '';
}

function validateKeyOptions(): string {
  if (form.keyType === 'RSA' && !validKeySizes.includes(form.keySize)) {
    return 'Key Size must be 2048, 3072, or 4096 when Key Type is RSA.';
  }

  if (form.keyType === 'EC' && !validKeyCurves.includes(form.keyCurveName)) {
    return 'Curve must be P-256, P-384, P-521, or P-256K when Key Type is EC.';
  }

  return '';
}

function validateTags(items: CertificateTagInput[]): TagValidationOutcome {
  const tags: Record<string, string> = {};
  const seenKeys = new Set<string>();

  for (const item of items) {
    const key = item.key.trim();
    const value = item.value.trim();

    if (!key && !value) {
      continue;
    }

    if (!key) {
      return { tags: {}, message: 'Tag name is required.' };
    }

    if (key.toLowerCase() === 'acmebot') {
      return { tags: {}, message: 'The Acmebot tag is managed by Acmebot.' };
    }

    const tagKey = key.toLowerCase();

    if (seenKeys.has(tagKey)) {
      return { tags: {}, message: 'Tag names must be unique.' };
    }

    seenKeys.add(tagKey);
    tags[key] = value;
  }

  return { tags, message: '' };
}

function addDnsName(dnsName: string, dnsProviderName: string): void {
  form.dnsNames.push(dnsName);
  form.dnsProviderName = dnsProviderName;
}

function removeDnsName(dnsName: string): void {
  form.dnsNames = form.dnsNames.filter((candidate) => candidate !== dnsName);

  if (form.dnsNames.length === 0) {
    form.dnsProviderName = activeIssueMode.value.useSelectedZoneProvider && selectedZone.value ? selectedZone.value.dnsProviderName : '';
  }
}

function submit(): void {
  if (form.dnsNames.length === 0) {
    return;
  }

  if (!canSubmit.value) {
    return;
  }

  const dnsAlias = activeIssueMode.value.useGeneratedDnsAlias ? delegatedDnsAlias.value : '';

  const policy: CertificatePolicyItem = {
    dnsNames: form.dnsNames,
    dnsProviderName: form.dnsProviderName,
    certificateName: effectiveCertificateName.value,
    keyType: form.keyType,
    reuseKey: form.useAdvancedOptions ? form.reuseKey : false,
    dnsAlias: dnsAlias || undefined,
  };

  if (form.keyType === 'RSA') {
    policy.keySize = form.keySize;
  } else {
    policy.keyCurveName = form.keyCurveName;
  }

  if (form.useAdvancedOptions && tagCount.value > 0) {
    policy.tags = tagValidation.value.tags;
  }

  if (form.useAdvancedOptions && form.profile.trim()) {
    policy.profile = form.profile.trim();
  }

  emit('submit', policy);
}
</script>

<template>
  <Teleport to="body">
    <div
      v-if="open"
      class="modal-shell"
      role="dialog"
      aria-modal="true"
      aria-labelledby="add-certificate-heading"
    >
      <button
        class="modal-scrim"
        type="button"
        title="Close issue certificate"
        :disabled="sending"
        @click="emit('close')"
      />
      <section class="modal-panel modal-panel--wide">
        <header class="modal-panel__header">
          <div>
            <div class="eyebrow">
              Certificate issuance
            </div>
            <h2 id="add-certificate-heading">
              Issue Certificate
            </h2>
          </div>
          <button
            class="icon-only-button"
            type="button"
            title="Close issue certificate"
            :disabled="sending"
            @click="emit('close')"
          >
            <X
              :size="18"
              aria-hidden="true"
            />
          </button>
        </header>

        <div class="wizard-layout">
          <aside
            class="setup-rail"
            aria-label="Certificate issue setup"
          >
            <div class="setup-rail__header">
              <span>Issue setup</span>
              <strong>{{ issueStatusLabel }}</strong>
            </div>
            <div
              class="setup-step"
              :class="{ 'is-complete': selectedZone }"
            >
              <ShieldPlus
                :size="17"
                aria-hidden="true"
              />
              <div class="setup-step__body">
                <span>{{ zoneStepLabel }}</span>
                <strong>{{ selectedZone ? displayDnsName(selectedZone.name) : 'Not selected' }}</strong>
                <small v-if="selectedZone">{{ selectedZone.dnsProviderName }}</small>
              </div>
            </div>
            <div
              class="setup-step"
              :class="{ 'is-complete': form.dnsNames.length > 0 }"
            >
              <CirclePlus
                :size="17"
                aria-hidden="true"
              />
              <div class="setup-step__body">
                <span>Names</span>
                <strong>{{ dnsNamesSummary }}</strong>
                <small>{{ dnsNameCountLabel }}</small>
              </div>
            </div>
            <div
              class="setup-step"
              :class="{ 'is-complete': form.useAdvancedOptions }"
            >
              <KeyRound
                :size="17"
                aria-hidden="true"
              />
              <div class="setup-step__body">
                <span>Key</span>
                <strong>{{ keySummary }}</strong>
                <small>{{ form.useAdvancedOptions ? 'Custom settings' : 'Default settings' }}</small>
              </div>
            </div>
            <div
              class="setup-step"
              :class="{ 'is-complete': tagCount > 0 }"
            >
              <Tag
                :size="17"
                aria-hidden="true"
              />
              <div class="setup-step__body">
                <span>Tags</span>
                <strong>{{ tagCountLabel }}</strong>
                <small>Key Vault</small>
              </div>
            </div>
          </aside>

          <div class="wizard-body">
            <div class="form-section">
              <span class="form-label">Validation method</span>
              <div
                class="segmented-control issue-mode-control"
                role="group"
                aria-label="Validation method"
              >
                <button
                  type="button"
                  :class="{ 'is-selected': form.issueMode === 'managed' }"
                  @click="form.issueMode = 'managed'"
                >
                  TXT record
                </button>
                <button
                  type="button"
                  :class="{ 'is-selected': form.issueMode === 'delegated' }"
                  @click="form.issueMode = 'delegated'"
                >
                  CNAME alias
                </button>
              </div>
            </div>

            <div class="form-section">
              <label class="form-label">{{ zoneLabel }}</label>
              <SearchableZoneSelect
                v-model:selected="selectedZone"
                :groups="zones"
                :loading="loadingZones"
              />
            </div>

            <component
              :is="dnsNameEditorComponent"
              :selected-zone="selectedZone"
              :dns-names="form.dnsNames"
              :dns-provider-name="form.dnsProviderName"
              @add-dns-name="addDnsName"
              @remove-dns-name="removeDnsName"
            />

            <div class="form-section form-section--inline">
              <label class="toggle-row">
                <input
                  v-model="form.useAdvancedOptions"
                  type="checkbox"
                >
                <span>Advanced options</span>
              </label>
            </div>

            <AdvancedCertificateOptions
              v-if="form.useAdvancedOptions"
              v-model:certificate-name="form.certificateName"
              v-model:key-type="form.keyType"
              v-model:key-size="form.keySize"
              v-model:key-curve-name="form.keyCurveName"
              v-model:reuse-key="form.reuseKey"
              v-model:profile="form.profile"
              v-model:tags="form.tags"
              :certificate-name-error="certificateNameError"
              :key-option-error="keyOptionError"
              :tag-error="tagError"
            />
          </div>
        </div>

        <footer class="modal-panel__footer">
          <div class="modal-panel__footer-meta">
            <span>{{ dnsNameCountLabel }}</span>
            <span>{{ keySummary }}</span>
            <span>{{ tagCountLabel }}</span>
          </div>
          <div class="modal-panel__footer-actions">
            <button
              class="secondary-button"
              type="button"
              :disabled="sending"
              @click="emit('close')"
            >
              Cancel
            </button>
            <button
              class="primary-button"
              type="button"
              :disabled="!canSubmit"
              @click="submit"
            >
              <ShieldPlus
                :size="17"
                aria-hidden="true"
              />
              <span>Issue Certificate</span>
            </button>
          </div>
        </footer>
      </section>
    </div>
  </Teleport>
</template>
