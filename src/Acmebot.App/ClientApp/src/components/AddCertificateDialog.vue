<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { CirclePlus, KeyRound, Plus, ShieldPlus, Tag, Trash2, X } from 'lucide-vue-next';

import type { CertificatePolicyItem, DnsZoneGroup, KeyCurveName, KeyType, SelectableDnsZone } from '@/api/types';
import { displayDnsName } from '@/utils/certificates';
import { createCertificateName, createDelegatedDnsAlias, validateOptionalDnsAlias } from '@/utils/dnsNames';

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
    zoneLabel: 'DNS Zone',
    zoneStepLabel: 'Zone',
    emptyStatusLabel: 'Select zone',
    showManualDnsAlias: true,
    useSelectedZoneProvider: false,
    useGeneratedDnsAlias: false,
  },
  delegated: {
    editor: DelegatedDnsNameEditor,
    zoneLabel: 'DNS Alias Zone',
    zoneStepLabel: 'Alias zone',
    emptyStatusLabel: 'Select alias zone',
    showManualDnsAlias: false,
    useSelectedZoneProvider: true,
    useGeneratedDnsAlias: true,
  },
} as const;

type IssueMode = keyof typeof issueModeOptions;

const selectedZone = ref<SelectableDnsZone | null>(null);
let certificateTagId = 0;

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
  dnsAlias: '',
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
const dnsAliasValidation = computed(() => (form.useAdvancedOptions && activeIssueMode.value.showManualDnsAlias ? validateOptionalDnsAlias(form.dnsAlias) : { value: '', message: '' }));
const dnsAliasError = computed(() => dnsAliasValidation.value.message);
const submitValidationMessage = computed(() => {
  if (form.dnsNames.length === 0) {
    return 'Add at least one DNS name.';
  }

  return certificateNameError.value || dnsAliasError.value || keyOptionError.value || tagError.value;
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

    if (!activeIssueMode.value.showManualDnsAlias) {
      form.dnsAlias = '';
    }
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
      form.dnsAlias = '';
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
  form.dnsAlias = '';
  form.tags = [];
}

function createTagInput(): CertificateTagInput {
  return { id: ++certificateTagId, key: '', value: '' };
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

function addTag(): void {
  if (form.tags.some((tag) => !tag.key.trim() && !tag.value.trim())) {
    return;
  }

  form.tags.push(createTagInput());
}

function removeTag(id: number): void {
  form.tags = form.tags.filter((tag) => tag.id !== id);
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

  const dnsAlias = activeIssueMode.value.useGeneratedDnsAlias ? delegatedDnsAlias.value : dnsAliasValidation.value.value;

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
              <span class="form-label">Issue Mode</span>
              <div
                class="segmented-control issue-mode-control"
                role="group"
                aria-label="Issue mode"
              >
                <button
                  type="button"
                  :class="{ 'is-selected': form.issueMode === 'managed' }"
                  @click="form.issueMode = 'managed'"
                >
                  Managed zone
                </button>
                <button
                  type="button"
                  :class="{ 'is-selected': form.issueMode === 'delegated' }"
                  @click="form.issueMode = 'delegated'"
                >
                  Delegated DNS-01
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

            <div
              v-if="form.useAdvancedOptions"
              class="advanced-grid"
            >
              <label
                class="form-field"
                :class="{ 'is-invalid': certificateNameError }"
              >
                <span class="form-label">Certificate Name</span>
                <input
                  v-model="form.certificateName"
                  type="text"
                  placeholder="Defaults to first DNS name"
                  :aria-invalid="certificateNameError ? 'true' : 'false'"
                >
                <span
                  v-if="certificateNameError"
                  class="form-error"
                >{{ certificateNameError }}</span>
              </label>

              <label
                class="form-field"
                :class="{ 'is-invalid': keyOptionError }"
              >
                <span class="form-label">Key Type</span>
                <select v-model="form.keyType">
                  <option value="RSA">RSA</option>
                  <option value="EC">EC</option>
                </select>
                <span
                  v-if="keyOptionError"
                  class="form-error"
                >{{ keyOptionError }}</span>
              </label>

              <label
                v-if="form.keyType === 'RSA'"
                class="form-field"
              >
                <span class="form-label">Key Size</span>
                <select v-model.number="form.keySize">
                  <option :value="2048">2048</option>
                  <option :value="3072">3072</option>
                  <option :value="4096">4096</option>
                </select>
              </label>

              <label
                v-else
                class="form-field"
              >
                <span class="form-label">Curve</span>
                <select v-model="form.keyCurveName">
                  <option value="P-256">P-256</option>
                  <option value="P-384">P-384</option>
                  <option value="P-521">P-521</option>
                  <option value="P-256K">P-256K</option>
                </select>
              </label>

              <label
                v-if="activeIssueMode.showManualDnsAlias"
                class="form-field"
                :class="{ 'is-invalid': dnsAliasError }"
              >
                <span class="form-label">DNS Alias</span>
                <input
                  v-model="form.dnsAlias"
                  type="text"
                  placeholder="alias.example.com"
                  :aria-invalid="dnsAliasError ? 'true' : 'false'"
                >
                <span
                  v-if="dnsAliasError"
                  class="form-error"
                >{{ dnsAliasError }}</span>
              </label>

              <label class="toggle-row advanced-grid__toggle">
                <input
                  v-model="form.reuseKey"
                  type="checkbox"
                >
                <span>Reuse key on renewal</span>
              </label>

              <div class="tag-editor advanced-grid__wide">
                <div class="tag-editor__header">
                  <span class="form-label">Key Vault Tags</span>
                  <button
                    class="secondary-button"
                    type="button"
                    @click="addTag"
                  >
                    <Plus
                      :size="16"
                      aria-hidden="true"
                    />
                    <span>Add tag</span>
                  </button>
                </div>
                <div
                  v-if="form.tags.length === 0"
                  class="tag-editor__empty"
                >
                  No tags
                </div>
                <div
                  v-else
                  class="tag-editor__rows"
                >
                  <div
                    v-for="tagItem in form.tags"
                    :key="tagItem.id"
                    class="tag-row"
                  >
                    <label
                      class="visually-hidden"
                      :for="`tag-key-${tagItem.id}`"
                    >Tag name</label>
                    <input
                      :id="`tag-key-${tagItem.id}`"
                      v-model="tagItem.key"
                      type="text"
                      placeholder="Name"
                      :aria-invalid="tagError ? 'true' : 'false'"
                    >
                    <label
                      class="visually-hidden"
                      :for="`tag-value-${tagItem.id}`"
                    >Tag value</label>
                    <input
                      :id="`tag-value-${tagItem.id}`"
                      v-model="tagItem.value"
                      type="text"
                      placeholder="Value"
                      :aria-invalid="tagError ? 'true' : 'false'"
                    >
                    <button
                      class="icon-only-button"
                      type="button"
                      title="Remove tag"
                      @click="removeTag(tagItem.id)"
                    >
                      <Trash2
                        :size="15"
                        aria-hidden="true"
                      />
                    </button>
                  </div>
                </div>
                <span
                  v-if="tagError"
                  class="form-error"
                >{{ tagError }}</span>
              </div>
            </div>
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
