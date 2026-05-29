import AppIntents
import SwiftUI
import WidgetKit
import StreakEngine

struct StreakStatusEntry: TimelineEntry {
    let date: Date
    let lastRunText: String
    let authRequired: Bool
}

struct StreakStatusProvider: TimelineProvider {
    func placeholder(in context: Context) -> StreakStatusEntry {
        StreakStatusEntry(date: Date(), lastRunText: "—", authRequired: false)
    }

    func getSnapshot(in context: Context, completion: @escaping (StreakStatusEntry) -> Void) {
        completion(makeEntry())
    }

    func getTimeline(in context: Context, completion: @escaping (Timeline<StreakStatusEntry>) -> Void) {
        let entry = makeEntry()
        let next = Calendar.current.date(byAdding: .hour, value: 1, to: Date()) ?? Date().addingTimeInterval(3600)
        completion(Timeline(entries: [entry], policy: .after(next)))
    }

    private func makeEntry() -> StreakStatusEntry {
        let settings = SharedSettings.shared
        let auth = settings.getBool(SharedConstants.authRequiredKey)
        let history = settings.getRunHistory()
        let lastText: String
        if let last = history.first {
            let formatter = RelativeDateTimeFormatter()
            lastText = formatter.localizedString(for: last.runTime, relativeTo: Date())
        } else {
            lastText = "Never"
        }
        return StreakStatusEntry(date: Date(), lastRunText: lastText, authRequired: auth)
    }
}

struct StreakWidgetView: View {
    var entry: StreakStatusEntry

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Image(systemName: "flame.fill")
                    .foregroundStyle(.pink)
                Text("Streak Saver")
                    .font(.headline)
            }
            if entry.authRequired {
                Text("Login required")
                    .font(.caption)
                    .foregroundStyle(.red)
            } else {
                Text("Last run: \(entry.lastRunText)")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Button(intent: MaintainStreakIntent()) {
                Label("Run now", systemImage: "paperplane.fill")
            }
            .buttonStyle(.borderedProminent)
            .tint(.pink)
        }
        .padding()
    }
}

@main
struct StreakWidgetBundle: WidgetBundle {
    var body: some Widget {
        StreakWidget()
    }
}

struct StreakWidget: Widget {
    let kind = "StreakSaverWidget"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: StreakStatusProvider()) { entry in
            StreakWidgetView(entry: entry)
                .containerBackground(.fill.tertiary, for: .widget)
        }
        .configurationDisplayName("Streak Saver")
        .description("Quick status and one-tap streak run.")
        .supportedFamilies([.systemSmall, .systemMedium])
    }
}
