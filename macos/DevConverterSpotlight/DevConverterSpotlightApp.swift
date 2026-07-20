import SwiftUI

@main
struct DevConverterSpotlightApp: App {
    var body: some Scene {
        WindowGroup {
            VStack(alignment: .leading, spacing: 12) {
                Text("DevConverter")
                    .font(.title)
                Text("The Spotlight action is installed.")
                Text("Assign the Quick Key “dev” to “Dev Convert” in System Settings → Spotlight → Quick Keys, then type commands such as “dev date now”.")
                    .foregroundStyle(.secondary)
                    .textSelection(.enabled)
            }
            .padding(24)
            .frame(width: 520)
        }
    }
}
