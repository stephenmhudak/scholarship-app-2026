<template>
  <v-card>
    <v-empty-state
      icon="mdi-file-document-outline"
      title="No application started"
      text="You haven't started an application yet."
    >
      <template #actions>
        <v-btn color="primary" prepend-icon="mdi-plus" to="/application/new">Start Application</v-btn>
      </template>
    </v-empty-state>
  </v-card>
</template>
