<script setup>
import { ref, onMounted, onBeforeUnmount, watch } from 'vue'
import DataTablesCore from 'datatables.net-dt'
import DataTablesVue from 'datatables.net-vue3'

DataTablesVue.use(DataTablesCore)

const props = defineProps({
  columns: {
    type: Array,
    required: true,
  },
  ajaxUrl: {
    type: String,
    default: null,
  },
  data: {
    type: Array,
    default: null,
  },
  options: {
    type: Object,
    default: () => ({}),
  },
})

const emit = defineEmits(['rowClick'])

const tableRef = ref(null)

const tableOptions = {
  responsive: true,
  pageLength: 25,
  ...props.options,
  columns: props.columns,
  ...(props.ajaxUrl
    ? {
        serverSide: true,
        ajax: {
          url: props.ajaxUrl,
          type: 'GET',
        },
      }
    : {}),
  ...(props.data ? { data: props.data } : {}),
}
</script>

<template>
  <div class="overflow-x-auto">
    <DataTablesVue
      ref="tableRef"
      class="display stripe hover w-full"
      :options="tableOptions"
      @click="(e) => {
        const row = e.target.closest('tr')
        if (row) emit('rowClick', row)
      }"
    />
  </div>
</template>
